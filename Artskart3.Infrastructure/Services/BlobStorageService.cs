using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["CsvExport:BlobStorage:ConnectionString"] ?? "UseDevelopmentStorage=true";
        _containerName = configuration["CsvExport:BlobStorage:ContainerName"] ?? "csv-exports";
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Et lettvekts-kall som tvinger en faktisk forespørsel mot blob storage.
            // Hvis tjenesten ikke svarer (f.eks. Azurite er ikke startet) kastes en exception.
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            await container.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Kan ikke koble til blob storage (konto: '{_blobServiceClient.AccountName}', container: '{_containerName}'). " +
                "Sjekk at Azurite kjører lokalt, eller at connection string ('CsvExport:BlobStorage:ConnectionString') er riktig.",
                ex);
        }
    }

    public async Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobPath);
        if (content.CanSeek)
            content.Position = 0;
        await blob.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenWriteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobPath);
        return await blob.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenReadStreamAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);
        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);

        if (!blob.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Kan ikke generere SAS-URL. Sjekk at connection string inneholder kontonøkkel.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blob.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}

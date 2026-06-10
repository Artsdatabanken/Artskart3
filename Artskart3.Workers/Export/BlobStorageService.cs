using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Workers.Configuration;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobStorageService(IOptions<CsvExportOptions> options)
    {
        var blobOptions = options.Value.BlobStorage;
        _blobServiceClient = new BlobServiceClient(blobOptions.ConnectionString);
        _containerName = blobOptions.ContainerName;
    }

    public async Task<Stream> OpenWriteStreamAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobPath);
        return await blob.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobPath);

        if (!blob.CanGenerateSasUri)
        {
            // Fallback: opprett ny klient med connection string som støtter SAS
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

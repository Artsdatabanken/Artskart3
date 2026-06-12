namespace Artskart3.Core.Application.Services.Interfaces;

public interface IBlobStorageService
{
    // TODO: Bytt tilbake til OpenWriteStreamAsync når Azurite-bug er fikset
    Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadStreamAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor);
    Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default);
}

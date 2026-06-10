namespace Artskart3.Core.Application.Services.Interfaces;

public interface IBlobStorageService
{
    Task<Stream> OpenWriteStreamAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor);
    Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default);
}

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IBlobStorageService
{
    /// <summary>
    /// Verifiserer at det er mulig å koble til blob storage (f.eks. at Azurite kjører lokalt).
    /// Kaster en beskrivende <see cref="InvalidOperationException"/> hvis tilkoblingen feiler.
    /// </summary>
    Task CheckConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Laster opp innholdet i en ferdig strøm. Egnet for filer som allerede finnes
    /// i minnet eller på disk.
    /// </summary>
    Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Åpner en skrivestrøm rett mot blob storage. Innholdet committes når strømmen
    /// lukkes. Brukes for store filer som ikke bør bufres i minnet.
    /// </summary>
    Task<Stream> OpenWriteAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadStreamAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor);
    Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default);
}

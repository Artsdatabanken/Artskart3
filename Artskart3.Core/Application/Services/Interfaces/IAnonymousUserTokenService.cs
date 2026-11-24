namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Utsteder og validerer signerte anonyme bruker-ID-er for eksport.
///
/// Signaturen er det som skiller en anonym ID fra en vilkårlig GUID: uten den
/// kunne hvem som helst sendt en annen brukers sub i X-Anonymous-User-Id-headeren
/// og lest, lastet ned eller overtatt eksportene deres.
/// </summary>
public interface IAnonymousUserTokenService
{
    /// <summary>Lager et nytt signert token med en fersk GUID.</summary>
    string CreateToken();

    /// <summary>
    /// Verifiserer signaturen og trekker ut GUID-en. Returnerer false for
    /// manglende, tukla eller utløpte token, og for Guid.Empty.
    /// </summary>
    bool TryGetUserId(string? token, out Guid userId);
}

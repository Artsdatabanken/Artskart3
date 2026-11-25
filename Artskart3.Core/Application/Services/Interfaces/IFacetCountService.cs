using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Regner ut antall observasjoner ved hvert valg i filterpanelet.
///
/// Velger kilde selv: fasett-bufferen når den dekker filteret, ellers live
/// aggregering. Kalleren merker ingen forskjell utover svartiden.
/// </summary>
public interface IFacetCountService
{
    /// <summary>
    /// Fasett-tall for de etterspurte dimensjonene og bool-filtrene.
    ///
    /// Ukjente nøkler i <paramref name="dimensionKeys"/> eller
    /// <paramref name="boolKeys"/> hoppes over framfor å feile — klienten og serveren
    /// kan være i utakt over en deploy, og et ukjent navn skal koste et manglende tall,
    /// ikke hele svaret.
    /// </summary>
    /// <param name="filter">Filteret slik det står i panelet.</param>
    /// <param name="dimensionKeys">Dimensjonsfasettene som skal hentes.</param>
    /// <param name="boolKeys">Bool-fasettene som skal hentes.</param>
    /// <param name="includeAreas">
    /// Om områdelistene skal telles. Egen bryter og ikke en dimensjonsnøkkel, fordi
    /// svaret nøkles på Fid og ikke på en bøtte-id.
    /// </param>
    /// <param name="cancellationToken">Avbrytelse.</param>
    Task<FacetCountsResponseDto> GetFacetCountsAsync(
        IObservationFilter filter,
        IReadOnlyList<string> dimensionKeys,
        IReadOnlyList<string> boolKeys,
        bool includeAreas = false,
        CancellationToken cancellationToken = default);
}

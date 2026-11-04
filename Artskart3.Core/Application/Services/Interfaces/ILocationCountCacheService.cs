using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Slår opp de mest observerte lokasjonene i kartutsnittet fra en ferdig
/// beregnet tabell, i stedet for å aggregere indekstabellen.
/// </summary>
public interface ILocationCountCacheService
{
    /// <summary>
    /// Returnerer de <paramref name="maxResults"/> mest observerte lokasjonene i
    /// utsnittet, eller <c>null</c> dersom filteret ikke kan besvares fra bufferen.
    ///
    /// <c>null</c> er ikke en feil — det er det normale utfallet for filtre
    /// bufferen ikke dekker (to eller flere dimensjoner, katalognummer, takson
    /// under ordensnivå) og for en buffer som ikke er bygget ennå. Kalleren
    /// aggregerer da som før.
    ///
    /// Rekkefølgen er antall synkende, deretter LocationId stigende — nøyaktig
    /// som tellestien. Tiebreakeren er ikke pynt: for Oslo-utsnittet uten filtre
    /// ligger grensen ved TOP 100 000 på to observasjoner, og 42 308 lokasjoner
    /// har nøyaktig to. Uten den ville bufferen og tellingen returnert ulike
    /// lokasjoner med like tall.
    ///
    /// Resultatet skal være identisk med tellingen, lokasjon for lokasjon. Er det
    /// ikke det, er bufferen feil — ikke rund av, ikke gjett.
    /// </summary>
    Task<List<LocationModel>?> TryGetTopLocationsAsync(
        LocationSearchFilterDto filter,
        int maxResults,
        CancellationToken cancellationToken = default);

}

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Slår opp forhåndsberegnede områdeantall i stedet for å telle observasjoner.
/// </summary>
public interface IAreaCountCacheService
{
    /// <summary>
    /// Returnerer antall per (områdetype, områdeid) fra bufferen, eller <c>null</c>
    /// dersom filteret ikke kan besvares derfra.
    ///
    /// <c>null</c> er ikke en feil — det er det normale utfallet for filtre bufferen
    /// ikke dekker (tre eller flere dimensjoner, katalognummer, takson under
    /// ordensnivå) og for en buffer som ikke er bygget ennå. Kalleren faller da
    /// tilbake til å telle.
    ///
    /// Resultatet skal være identisk med det tellingen ville gitt, celle for celle.
    /// Er det ikke det, er bufferen feil — ikke rund av, ikke gjett.
    /// </summary>
    /// <param name="filter">Filteret slik det kom inn.</param>
    /// <param name="areas">
    /// Områdene som skal telles, allerede innsnevret av et eventuelt fylke-, kommune-
    /// eller havområdevalg. Bestemmer hvilke celler som returneres, ikke hvilke
    /// observasjoner som teller med.
    /// </param>
    /// <param name="areasAreNarrowed">
    /// Om <paramref name="areas"/> faktisk ble kortere av et områdevalg. Er den ikke
    /// det, inneholder den alle områdene for sine typer, og id-lista trenger ikke sendes
    /// til databasen i det hele tatt — den fjerner ingen rader og koster én navngitt
    /// parameter per id. Se AreaCountCacheService.
    /// </param>
    /// <param name="cancellationToken">Avbrytelse.</param>
    Task<Dictionary<(int EntityTypeId, int EntityId), int>?> TryGetCountsAsync(
        LocationSearchFilterDto filter,
        List<Area> areas,
        bool areasAreNarrowed,
        CancellationToken cancellationToken = default);
}

using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Slår opp forhåndsberegnede fasett-antall i stedet for å aggregere.
///
/// Fasett-tallet er antall observasjoner som ville vært i resultatet hvis brukeren
/// valgte ett gitt alternativ — gjeldende filter MINUS dimensjonen alternativet hører
/// til. Se <see cref="FacetCountCacheDimensions"/> for semantikken og dekningen.
/// </summary>
public interface IFacetCountCacheService
{
    /// <summary>
    /// Antall per bøtte for én dimensjonsfasett, eller <c>null</c> dersom filteret
    /// ikke kan besvares fra bufferen.
    ///
    /// <c>null</c> er ikke en feil — det er det normale utfallet for filtre bufferen
    /// ikke dekker (to eller flere andre aktive dimensjoner, katalognummer,
    /// matrikkelenheter, takson under ordensnivå) og for en buffer som ikke er bygget
    /// ennå. Kalleren faller da tilbake til å aggregere.
    ///
    /// En TOM ordbok betyr noe annet: filteret treffer ingen observasjoner, så hvert
    /// alternativ er null. Kalleren skal vise 0 for bøtter som ikke står i ordboken,
    /// ikke tolke fraværet som «ukjent».
    ///
    /// Resultatet skal være identisk med det aggregeringen ville gitt, bøtte for
    /// bøtte. Er det ikke det, er bufferen feil — ikke rund av, ikke gjett.
    /// </summary>
    Task<Dictionary<int, int>?> TryGetDimensionCountsAsync(
        FacetDimension facet,
        IObservationFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Antallet for ETT bool-alternativ — «med bilde», «uten bilde», «sensitiv» —
    /// eller <c>null</c> dersom filteret ikke kan besvares fra bufferen.
    ///
    /// Brukerens eget valg på samme bit overstyres: tallet svarer på «hva får du hvis
    /// du velger denne i stedet». Maskefasettene dekkes ett hakk lenger enn
    /// dimensjonsfasettene, siden de ikke bruker opp en bøttedimensjon selv.
    /// </summary>
    Task<int?> TryGetMaskCountAsync(
        FacetMaskOption facet,
        bool value,
        IObservationFilter filter,
        CancellationToken cancellationToken = default);
}

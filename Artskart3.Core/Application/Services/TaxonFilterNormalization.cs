using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Normalisering av taksonutvalg før det brukes som filter.
/// </summary>
public static class TaxonFilterNormalization
{
    /// <summary>
    /// Fjerner taxonId-er som er etterkommere av et annet taxonId i samme utvalg.
    /// Velges Dyreriket og torsk, blir torsk borte — Dyreriket dekker den allerede.
    ///
    /// SEMANTISK IDENTISK MED FØR
    /// Filtrene ORer nivåene sammen på samme rad, så en observasjon som matcher både
    /// forfar og etterkommer telles én gang uansett. Dette er en normalisering, ikke
    /// en endring av hva som returneres.
    ///
    /// HVORFOR DEN LIKEVEL TRENGS
    /// Områdebufferen har ett tall per (takson, område) per rangnivå. Forfar og
    /// etterkommer havner i hver sin nivåtabell, og summen ville dobbelttalt nettopp
    /// de observasjonene. Uten normaliseringen måtte bufferen gitt opp hver gang et
    /// utvalg spente over flere rangnivåer.
    ///
    /// Frontend skal ikke kunne produsere slike utvalg via taksontreet, men artssøket
    /// gikk utenom (<c>filterState.addTaxon</c> uten forfedre-sjekk). API-et kan
    /// uansett ikke stole på at klienten rydder.
    ///
    /// KOSTNAD
    /// Ett <see cref="ITaxonHierarchyService.GetAncestries"/>-kall mot minnestrukturen.
    /// Utvalg på ett element — det store flertallet — returnerer uten å røre den.
    /// </summary>
    /// <returns>
    /// Samme rekkefølge som inndata, uten duplikater og uten etterkommere. Null og
    /// tom liste returneres uendret slik at kallerens egne tomhetssjekker oppfører seg likt.
    /// </returns>
    public static int[]? RemoveRedundantDescendants(
        this ITaxonHierarchyService taxonHierarchy, int[]? taxonIds)
    {
        if (taxonIds is null || taxonIds.Length <= 1)
            return taxonIds;

        var distinct = taxonIds.Distinct().ToArray();
        if (distinct.Length <= 1)
            return distinct;

        var selected = distinct.ToHashSet();

        // Ukjente taxa får tom kjede og beholdes. De gir tomt resultat lenger nede
        // uansett, og å fjerne dem her ville skjult en feil i stedet for å vise den.
        var ancestorsById = taxonHierarchy.GetAncestries(distinct)
            .ToDictionary(a => a.Id, a => a.ParentIds);

        return distinct
            .Where(id => !(ancestorsById.TryGetValue(id, out var ancestors)
                           && ancestors.Any(selected.Contains)))
            .ToArray();
    }
}

using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Kjører fasett-aggregeringer samtidig, hver på sin egen databasetilkobling.
///
/// HVORFOR DEN FINNES
/// Aggregeringene er fall-through-stien når bufferen ikke dekker filteret, og
/// sekvensielt blir de summen av seg selv. Målt på produksjonslik data med filteret
/// taksongruppe 8 + kategori 9 — begge brede — er hver aggregering 2,4-2,6 s, og med
/// alle seks gruppene åpne i filterpanelet ble kallet 9,4 s.
///
/// En DbContext tåler bare én operasjon om gangen, så de kan ikke parallelliseres på
/// den som følger forespørselen. Her får hver jobb sin egen, laget fra de samme
/// registrerte innstillingene.
///
/// HVORFOR ET TAK
/// Arbeidet forsvinner ikke av å overlappes — det er fortsatt flere
/// COUNT(DISTINCT)-skanninger over titalls millioner rader, på en delt database. Taket
/// er grunnen til å gjøre dette serverside framfor å sende ett HTTP-kall per gruppe
/// fra klienten: samtidigheten blir noe vi styrer, ikke noe nettleseren bestemmer.
/// Søke-endepunktene deler dessuten én global concurrency limiter på 32 plasser.
/// </summary>
public interface IFacetAggregationRunner
{
    /// <summary>
    /// Aggregerer fasett-tallene for flere dimensjoner. Svaret kommer i samme
    /// rekkefølge som <paramref name="facets"/>.
    /// </summary>
    Task<IReadOnlyList<Dictionary<int, int>>> AggregateDimensionsAsync(
        IObservationFilter filter,
        IReadOnlyList<FacetDimension> facets,
        FilterMask.Selection mask,
        int[]? cadastralLocationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregerer totalen for flere maskeutvalg — ett per bool-alternativ. Svaret
    /// kommer i samme rekkefølge som <paramref name="masks"/>.
    /// </summary>
    Task<IReadOnlyList<int>> AggregateTotalsAsync(
        IObservationFilter filter,
        IReadOnlyList<FilterMask.Selection> masks,
        int[]? cadastralLocationIds,
        CancellationToken cancellationToken = default);
}

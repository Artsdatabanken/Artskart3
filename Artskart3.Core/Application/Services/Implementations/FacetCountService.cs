using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Core.Application.Services.Implementations;

/// <summary>
/// Antall observasjoner ved hvert valg i filterpanelet.
///
/// KILDEVALGET
/// Fasett-bufferen spørres først, for hver gruppe. Svarer den <c>null</c>, dekker den
/// ikke filteret, og tallet må aggregeres i stedet.
///
/// TO FASER, OG DET ER POENGET
/// Bufferoppslagene er 4-7 ms og kjøres sekvensielt på forespørselens egen context.
/// Aggregeringene er en helt annen størrelsesorden — målt 2,4-2,6 s hver med et bredt
/// filter — så de samles opp og kjøres SAMTIDIG av
/// <see cref="IFacetAggregationRunner"/>, hver på sin egen tilkobling og med et tak på
/// hvor mange som går om gangen.
///
/// Sekvensielt ble kallet 9,4 s med alle seks gruppene åpne og filteret taksongruppe 8
/// + kategori 9. Planen antok at to eller flere aktive filtre innebar selektivitet;
/// det gjør de ikke når begge er brede.
///
/// ÉN SPØRRING PER FASETT, IKKE ÉN SAMLET
/// Målt: seks fasetter i én GROUPING SETS-setning tok 7631 ms, mot 841 ms for de samme
/// seks som enkeltspørringer — <c>COUNT(DISTINCT)</c> faller ut av batch mode i den
/// samlede formen.
///
/// ÉN FORESPØRSEL, UANSETT HVOR MANGE GRUPPER
/// Klienten får alt i ett HTTP-kall. Alternativet — ett kall per accordion — ville gitt
/// samme parallellitet, men multiplisert auth, DI-oppsett og matrikkeloppslaget med
/// antall grupper, og brukt like mange plasser i den globale rate limiteren.
/// </summary>
public class FacetCountService : IFacetCountService
{
    private const string FromCache = "cache";
    private const string FromAggregation = "aggregated";

    private readonly IFacetCountCacheService _cache;
    private readonly ISearchRepository _repository;
    private readonly IFacetAggregationRunner _aggregationRunner;
    private readonly ILogger<FacetCountService> _logger;

    public FacetCountService(
        IFacetCountCacheService cache,
        ISearchRepository repository,
        IFacetAggregationRunner aggregationRunner,
        ILogger<FacetCountService> logger)
    {
        _cache = cache;
        _repository = repository;
        _aggregationRunner = aggregationRunner;
        _logger = logger;
    }

    /// <summary>En gruppe som venter på å bli aggregert, med plassen sin i svaret.</summary>
    private sealed record PendingDimension(int Index, FacetDimension Facet);

    private sealed record PendingBool(int Index, bool Value, FilterMask.Selection Mask);

    /// <inheritdoc />
    public async Task<FacetCountsResponseDto> GetFacetCountsAsync(
        IObservationFilter filter,
        IReadOnlyList<string> dimensionKeys,
        IReadOnlyList<string> boolKeys,
        bool includeAreas = false,
        CancellationToken cancellationToken = default)
    {
        var mask = FilterMask.Select(filter);

        // --- Fase 1: bufferen, sekvensielt ------------------------------------
        var dimensions = new List<FacetCountsDto>();
        var ventendeDimensjoner = new List<PendingDimension>();

        foreach (var key in dimensionKeys.Distinct(StringComparer.Ordinal))
        {
            var facet = FacetCountCacheDimensions.FindDimension(key);
            if (facet is null)
            {
                // Ukjent navn koster ett manglende tall, ikke hele svaret: klient og
                // server kan være i utakt over en deploy.
                _logger.LogWarning("Ukjent fasettdimensjon «{Key}» ble etterspurt og hoppes over.", key);
                continue;
            }

            var fromCache = await _cache.TryGetDimensionCountsAsync(facet, filter, cancellationToken);

            // Plassen reserveres nå, slik at svaret beholder rekkefølgen klienten ba om
            // selv om aggregeringene fullfører i en annen.
            dimensions.Add(ToDto(facet.Key, fromCache ?? [], FromCache));

            if (fromCache is null)
                ventendeDimensjoner.Add(new PendingDimension(dimensions.Count - 1, facet));
        }

        var boolFilters = new List<FacetBoolCountsDto>();
        var ventendeBool = new List<PendingBool>();

        foreach (var key in boolKeys.Distinct(StringComparer.Ordinal))
        {
            var facet = FacetCountCacheDimensions.FindMaskFacet(key);
            if (facet is null)
            {
                _logger.LogWarning("Ukjent bool-fasett «{Key}» ble etterspurt og hoppes over.", key);
                continue;
            }

            var naar = await _cache.TryGetMaskCountAsync(facet, true, filter, cancellationToken);
            var ikke = await _cache.TryGetMaskCountAsync(facet, false, filter, cancellationToken);

            boolFilters.Add(new FacetBoolCountsDto
            {
                Key = facet.Key,
                WhenTrue = naar ?? 0,
                WhenFalse = ikke ?? 0,
            });

            var index = boolFilters.Count - 1;

            // Maskeutvalget regnes her også for aggregeringsstien, slik at begge
            // kildene ser nøyaktig det samme valget — brukerens eget valg på samme bit
            // overstyrt.
            if (naar is null)
                ventendeBool.Add(new PendingBool(index, true, FilterMask.WithBit(mask, facet.Bit, true)));

            if (ikke is null)
                ventendeBool.Add(new PendingBool(index, false, FilterMask.WithBit(mask, facet.Bit, false)));
        }

        // --- Fase 2: aggregeringene, samtidig ---------------------------------
        if (ventendeDimensjoner.Count > 0 || ventendeBool.Count > 0)
        {
            // Matrikkelenhetene slås opp ÉN gang og deles. Oppslaget går mot Kartverket,
            // og hver aggregering ville ellers gjort sitt eget.
            var cadastralLocationIds = await _repository.ResolveCadastralLocationIdsAsync(filter, cancellationToken);

            if (ventendeDimensjoner.Count > 0)
            {
                var resultater = await _aggregationRunner.AggregateDimensionsAsync(
                    filter,
                    ventendeDimensjoner.Select(v => v.Facet).ToList(),
                    mask,
                    cadastralLocationIds,
                    cancellationToken);

                for (var i = 0; i < ventendeDimensjoner.Count; i++)
                {
                    var ventende = ventendeDimensjoner[i];
                    dimensions[ventende.Index] = ToDto(ventende.Facet.Key, resultater[i], FromAggregation);
                }
            }

            if (ventendeBool.Count > 0)
            {
                var totaler = await _aggregationRunner.AggregateTotalsAsync(
                    filter,
                    ventendeBool.Select(v => v.Mask).ToList(),
                    cadastralLocationIds,
                    cancellationToken);

                for (var i = 0; i < ventendeBool.Count; i++)
                {
                    var ventende = ventendeBool[i];
                    var gjeldende = boolFilters[ventende.Index];

                    boolFilters[ventende.Index] = ventende.Value
                        ? new FacetBoolCountsDto { Key = gjeldende.Key, WhenTrue = totaler[i], WhenFalse = gjeldende.WhenFalse }
                        : new FacetBoolCountsDto { Key = gjeldende.Key, WhenTrue = gjeldende.WhenTrue, WhenFalse = totaler[i] };
                }
            }
        }

        // --- Områdelistene ----------------------------------------------------
        //
        // Egen vei, ikke en dimensjonsfasett. Geografi er flerverdi i bufferen — én
        // bøtte er HELE settet av områder en observasjon tilhører — så et tall per
        // område kan ikke leses rett ut av en bøtteliste. Repositoriet gjenbruker i
        // stedet tellestien til GetAreaCounts, som alt kan fordele på områder.
        var areas = new List<FacetAreaCountDto>();

        if (includeAreas)
        {
            var counts = await _repository.GetAreaFacetCountsAsync(filter, cancellationToken);

            areas = counts
                .OrderBy(c => c.Key, StringComparer.Ordinal)
                .Select(c => new FacetAreaCountDto { Fid = c.Key, Count = c.Value })
                .ToList();
        }

        return new FacetCountsResponseDto
        {
            Dimensions = dimensions,
            BoolFilters = boolFilters,
            Areas = areas,
        };
    }

    /// <summary>
    /// Bøtter uten treff utelates — klienten viser 0 for det som mangler. Sortert på
    /// bøtte-id, så svaret er stabilt mellom kall.
    /// </summary>
    private static FacetCountsDto ToDto(string key, Dictionary<int, int> counts, string source) =>
        new()
        {
            Key = key,
            Source = source,
            Buckets = counts
                .Where(b => b.Value > 0)
                .OrderBy(b => b.Key)
                .Select(b => new FacetBucketDto { Id = b.Key, Count = b.Value })
                .ToList(),
        };
}

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.QueryBuilders;

/// <summary>
/// Områdevalgene som slås opp via lokasjonene i stedet for ObservationEntityIndex:
/// matrikkelenheter, som er lokasjons-ID-er utledet av Kartverkets geometri, og
/// historiske områder, som er koblet til lokasjonene i HistoricalAreaLocation.
///
/// Matrikkelenhetene sendes som én JSON-parameter. De historiske områdene blir en
/// delspørring mot tabellen: et historisk fylke er flere hundre tusen lokasjoner, og som
/// JSON-liste tok en telling 1,8-3,4 s mot 243 ms for delspørringen (målt lokalt).
/// </summary>
public sealed record LocationSelection(int[]? CadastralLocationIds, int[]? HistoricalAreaIds)
{
    /// <summary>
    /// Utvalget for filteret, eller null når filteret ikke har noen av delene.
    /// </summary>
    public static async Task<LocationSelection?> ResolveAsync(
        IObservationFilter filter, ICadastralUnitService cadastralUnits, CancellationToken cancellationToken)
    {
        var cadastralLocationIds = filter.CadastralUnitIds?.Length > 0
            ? await cadastralUnits.GetLocationIdsAsync(filter.CadastralUnitIds, cancellationToken)
            : null;
        var historicalAreaIds = filter.HistoricalAreaIds?.Length > 0 ? filter.HistoricalAreaIds : null;

        return cadastralLocationIds == null && historicalAreaIds == null
            ? null
            : new LocationSelection(cadastralLocationIds, historicalAreaIds);
    }

    /// <summary>
    /// Observasjonene på lokasjonene i utvalget.
    ///
    /// Med begge delene blir det OR mellom dem, og en observasjon som treffer begge kommer
    /// bare med én gang. UNION ALL av observasjons-ID-ene er prøvd og forkastet: listen ble
    /// ikke raskere, og områdetellingene mot indekstabellen gikk fra 352 til 1 875 ms.
    /// </summary>
    public IQueryable<Observation> Apply(IArtsKartDbContext context, IQueryable<Observation> query)
    {
        var cadastral = CadastralLocationIds;
        var historical = HistoricalLocationIds(context);

        if (historical == null)
            return query.Where(o => o.LocationId != null && EF.Parameter(cadastral!).Contains(o.LocationId.Value));

        if (cadastral == null)
            return query.Where(o => o.LocationId != null && historical.Contains(o.LocationId.Value));

        return query.Where(o => o.LocationId != null &&
            (EF.Parameter(cadastral).Contains(o.LocationId.Value) || historical.Contains(o.LocationId.Value)));
    }

    /// <summary>
    /// Indeksradene på lokasjonene i utvalget. Samme regel som for observasjonene.
    /// </summary>
    public IQueryable<ObservationEntityIndex> Apply(IArtsKartDbContext context, IQueryable<ObservationEntityIndex> query)
    {
        var cadastral = CadastralLocationIds;
        var historical = HistoricalLocationIds(context);

        if (historical == null)
            return query.Where(idx => idx.LocationId != null && EF.Parameter(cadastral!).Contains(idx.LocationId.Value));

        if (cadastral == null)
            return query.Where(idx => idx.LocationId != null && historical.Contains(idx.LocationId.Value));

        return query.Where(idx => idx.LocationId != null &&
            (EF.Parameter(cadastral).Contains(idx.LocationId.Value) || historical.Contains(idx.LocationId.Value)));
    }

    private IQueryable<int>? HistoricalLocationIds(IArtsKartDbContext context)
    {
        var areaIds = HistoricalAreaIds;
        return areaIds == null
            ? null
            : context.Set<HistoricalAreaLocation>().Where(h => areaIds.Contains(h.AreaId)).Select(h => h.LocationId);
    }
}

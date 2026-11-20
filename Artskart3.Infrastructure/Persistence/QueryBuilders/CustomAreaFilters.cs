using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Services;

namespace Artskart3.Infrastructure.Persistence.QueryBuilders;

/// <summary>
/// Det egendefinerte området i et filter, slik søket og eksporten leser det.
///
/// Når <see cref="ObservationFilterValidator.CanSplitCustomArea"/> tillater det, uttrykkes
/// området som områdene helt innenfor det, pluss kantlokasjonene som OR-es med dem slik
/// matrikkelenhetene gjør, se <see cref="CustomAreaSplit"/>. Da sendes bare kanten som
/// lokasjonsliste, og resten er et vanlig områdefilter: områdebufferen, opprullingen og
/// UNION ALL-formen virker som ellers. Uten områder helt innenfor er filteret uendret.
///
/// Kanten bæres som én matrikkelenhet med en egen nøkkel foran polygonet, og
/// <see cref="LocationSelection.ResolveAsync"/> henter den i stedet for å spørre Kartverket.
/// </summary>
public static class CustomAreaFilters
{
    private const string EdgePrefix = "custom-area-edge:";

    public static bool IsEdge(IObservationFilter filter) =>
        filter.CadastralUnitIds is [var key] && key.StartsWith(EdgePrefix, StringComparison.Ordinal);

    public static async Task<LocationSearchFilterDto> SplitAsync(
        LocationSearchFilterDto filter, ICustomAreaService customAreas, CancellationToken cancellationToken) =>
        await FindSplitAsync(filter, customAreas, cancellationToken) is { } areas
            ? filter.WithCustomAreaAsAreas(areas.Municipalities, areas.OceanAreas, areas.Counties, EdgePrefix + filter.CustomAreaWkt)
            : filter;

    public static async Task<ObservationSearchFilterDto> SplitAsync(
        ObservationSearchFilterDto filter, ICustomAreaService customAreas, CancellationToken cancellationToken) =>
        await FindSplitAsync(filter, customAreas, cancellationToken) is { } areas
            ? filter.WithCustomAreaAsAreas(areas.Municipalities, areas.OceanAreas, areas.Counties, EdgePrefix + filter.CustomAreaWkt)
            : filter;

    /// <summary>
    /// Kantlokasjonene til et oppdelt område. Venter på at listen er lagret, så spørringen kan
    /// joine den, se <see cref="LocationListStore"/>.
    /// </summary>
    public static async Task<int[]> ResolveEdgeAsync(
        IObservationFilter filter, ICustomAreaService customAreas, CancellationToken cancellationToken)
    {
        var wkt = filter.CadastralUnitIds![0][EdgePrefix.Length..];
        var edge = (await customAreas.GetSplitAsync(wkt, cancellationToken))?.EdgeLocationIds
                   ?? throw new InvalidOperationException(ObservationFilterValidator.CustomAreaTooManyLocationsMessage);
        await LocationListStore.WaitForSaveAsync(edge, cancellationToken);
        return edge;
    }

    /// <summary>
    /// Lokasjonene i et egendefinert område som ikke er delt, eller null når filteret ikke har
    /// noe. Bufret per polygon, så flere oppslag i samme forespørsel koster ett.
    /// </summary>
    public static async Task<int[]?> ResolveLocationIdsAsync(
        IObservationFilter filter, ICustomAreaService customAreas, CancellationToken cancellationToken)
    {
        if (filter.CustomAreaWkt == null) return null;

        var locationIds = await customAreas.GetLocationIdsAsync(filter.CustomAreaWkt, cancellationToken)
                          // Kontrollerne avviser for store områder før de kommer hit.
                          ?? throw new InvalidOperationException(ObservationFilterValidator.CustomAreaTooManyLocationsMessage);
        await LocationListStore.WaitForSaveAsync(locationIds, cancellationToken);
        return locationIds;
    }

    private static async Task<(string[] Municipalities, string[] OceanAreas, string[] Counties)?> FindSplitAsync(
        IObservationFilter filter, ICustomAreaService customAreas, CancellationToken cancellationToken)
    {
        if (!ObservationFilterValidator.CanSplitCustomArea(filter)) return null;

        var split = await customAreas.GetSplitAsync(filter.CustomAreaWkt!, cancellationToken);
        if (split?.EdgeLocationIds == null) return null;

        string[] FidsOf(AreaType type) => split.InsideAreas.Where(a => a.AreaTypeId == (int)type).Select(a => a.Fid).ToArray();
        return (FidsOf(AreaType.Municipality), FidsOf(AreaType.OceanArea),
            [.. FidsOf(AreaType.County), .. FidsOf(AreaType.SvalbardBjørnøyaAndJanMayen)]);
    }
}

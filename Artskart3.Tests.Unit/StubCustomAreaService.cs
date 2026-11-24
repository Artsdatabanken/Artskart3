using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Stub uten database: polygonet slås opp i en fast tabell over lokasjoner.
/// Ukjente polygoner har ingen lokasjoner, og null betyr for mange. Bare lokasjonene i
/// <paramref name="locationIdsWithAreas"/> regnes som å ha områderader; de andre hentes fra
/// Observation, som testdataene uten ekte kommunerader krever.
/// </summary>
internal sealed class StubCustomAreaService(
    Dictionary<string, int[]?>? locationIdsByWkt = null,
    Dictionary<string, CustomAreaSplit>? splitsByWkt = null,
    int[]? locationIdsWithAreas = null) : ICustomAreaService
{
    private readonly Dictionary<string, CustomAreaSplit> _splitsByWkt = splitsByWkt ?? [];
    private readonly Dictionary<string, int[]?> _locationIdsByWkt = locationIdsByWkt ?? [];
    private readonly HashSet<int> _locationIdsWithAreas = [.. locationIdsWithAreas ?? []];

    public Task<int[]?> GetLocationIdsAsync(string wkt, CancellationToken cancellationToken = default) =>
        Task.FromResult(_locationIdsByWkt.GetValueOrDefault(wkt, []));

    public Task<CustomAreaSplit?> GetSplitAsync(string wkt, CancellationToken cancellationToken = default) =>
        Task.FromResult(_splitsByWkt.GetValueOrDefault(wkt));

    public Task<int[]> GetLocationsWithoutAreasAsync(int[] locationIds, CancellationToken cancellationToken = default) =>
        Task.FromResult(locationIds.Where(id => !_locationIdsWithAreas.Contains(id)).ToArray());
}

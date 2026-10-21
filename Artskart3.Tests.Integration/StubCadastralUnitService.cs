using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Integration;

/// <summary>
/// Stub uten Kartverket: matrikkelnummer slås opp i en fast tabell over lokasjoner.
/// Ukjente matrikkelnummer har ingen lokasjoner.
/// </summary>
internal sealed class StubCadastralUnitService(Dictionary<string, int[]>? locationIdsById = null) : ICadastralUnitService
{
    private readonly Dictionary<string, int[]> _locationIdsById = locationIdsById ?? [];

    public Task<CadastralUnitDto?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_locationIdsById.ContainsKey(id)
            ? new CadastralUnitDto
            {
                Id = id,
                WktPolygon = "MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)))",
                Extent = new MapExtentDto { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1 },
            }
            : null);

    public Task<int[]> GetLocationIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(ids.SelectMany(id => _locationIdsById.GetValueOrDefault(id, [])).Distinct().ToArray());
}

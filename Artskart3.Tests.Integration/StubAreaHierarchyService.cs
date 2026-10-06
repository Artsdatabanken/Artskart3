using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Integration;

/// <summary>
/// Enkel stub som implementerer IAreaHierarchyService med ren string-parsing.
/// Brukes i tester som ikke trenger ekte områdehierarki fra databasen.
/// </summary>
internal sealed class StubAreaHierarchyService : IAreaHierarchyService
{
    public string? GetCountyFid(string municipalityFid) =>
        municipalityFid.PadLeft(4, '0')[..2];

    public IReadOnlyList<string> GetMunicipalityFids(string countyFid) =>
        Array.Empty<string>();

    public int? FidToEntityId(string fid) =>
        int.TryParse(fid.Replace("_", ""), out var id) ? id : null;

    public int? RestrictedAreaFidToEntityId(string fid) =>
        int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : null;

    public int[] FidsToEntityIds(string[]? fids) =>
        fids?.Select(f => FidToEntityId(f)).Where(id => id.HasValue).Select(id => id!.Value).ToArray()
        ?? Array.Empty<int>();

    public int[] RestrictedAreaFidsToEntityIds(string[]? fids) =>
        fids?.Select(f => RestrictedAreaFidToEntityId(f)).Where(id => id.HasValue).Select(id => id!.Value).ToArray()
        ?? Array.Empty<int>();
    /// <summary>
    /// Omraadeboksene lastes fra databasen i den ekte tjenesten. Her holdes de i en
    /// dictionary testen kan fylle; tom betyr "ingen luking", som er samme
    /// oppfoersel som tjenesten har foer foerste last.
    /// </summary>
    public Dictionary<(int EntityTypeId, int EntityId), AreaBounds> Bounds { get; } = new();

    public AreaBounds? GetAreaBounds(int entityTypeId, int entityId)
        => Bounds.TryGetValue((entityTypeId, entityId), out var b) ? b : null;

    public int[] PruneToEnvelope(int[] entityIds, AreaBounds envelope, params int[] entityTypeIds)
        => AreaBoundsPruner.Prune(entityIds, envelope, Bounds, entityTypeIds);

    /// <summary>
    /// Samme regel som tjenesten: tom boksoversikt betyr «vet ikke», og da
    /// beholdes ID-ene. Testene fyller Bounds naar de vil styre utfallet.
    /// </summary>
    public int[] FilterToExistingAreas(int[] entityIds, int entityTypeId)
        => Bounds.Count == 0 ? entityIds : entityIds.Where(id => Bounds.ContainsKey((entityTypeId, id))).ToArray();
}

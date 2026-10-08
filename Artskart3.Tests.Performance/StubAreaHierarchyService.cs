using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Enkel stub som implementerer IAreaHierarchyService med ren string-parsing.
/// Brukes i benchmarks som ikke trenger ekte områdehierarki fra databasen.
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
    /// Samme regel som tjenesten: tom oversikt betyr «vet ikke», og da beholdes
    /// ID-ene. Testene fyller ExistingAreas naar de vil styre utfallet.
    ///
    /// ATSKILT FRA Bounds, som i tjenesten. Boksene er utledet av observasjonsdata,
    /// mens eksistens leses fra Area-tabellen. Et omraade uten observasjoner med
    /// lokasjon har ingen boks, men finnes.
    /// </summary>
    public HashSet<(int EntityTypeId, int EntityId)> ExistingAreas { get; } = [];

    public int[] FilterToExistingAreas(int[] entityIds, int entityTypeId)
        => ExistingAreas.Count == 0
            ? entityIds
            : entityIds.Where(id => ExistingAreas.Contains((entityTypeId, id))).ToArray();
}

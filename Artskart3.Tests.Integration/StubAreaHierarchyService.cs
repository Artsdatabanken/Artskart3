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
}

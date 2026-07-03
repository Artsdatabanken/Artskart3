using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Integration;

/// <summary>
/// Enkel stub som implementerer ITaxonHierarchyService.
/// Returnerer alltid TaxonRankId 22 (species) og tom barneliste.
/// </summary>
internal sealed class StubTaxonHierarchyService : ITaxonHierarchyService
{
    public int? GetTaxonRankId(int taxonId) => 22;

    public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId) => [];
}

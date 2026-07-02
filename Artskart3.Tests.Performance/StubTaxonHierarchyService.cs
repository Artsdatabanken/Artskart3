using Artskart3.Core.Application.Services.Interfaces;

namespace Artskart3.Tests.Performance;

/// <summary>
/// Enkel stub som implementerer ITaxonHierarchyService.
/// Returnerer alltid TaxonRankId 22 (species) med mindre annet er konfigurert.
/// </summary>
internal sealed class StubTaxonHierarchyService : ITaxonHierarchyService
{
    public int? GetTaxonRankId(int taxonId) => 22;
}

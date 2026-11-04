using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tester for <see cref="TaxonFilterNormalization.RemoveRedundantDescendants"/>.
///
/// Hierarkiet testene bruker:
///   1 Dyreriket
///     └ 10 Strålefinnede fisker
///         └ 100 Torskefisker
///             └ 1000 Torsk
///   2 Planteriket (egen gren, ingen slektskap med 1)
/// </summary>
public class TaxonFilterNormalizationTests
{
    private static readonly Dictionary<int, List<int>> Ancestors = new()
    {
        [1] = [],
        [10] = [1],
        [100] = [1, 10],
        [1000] = [1, 10, 100],
        [2] = [],
    };

    private sealed class Hierarchy : ITaxonHierarchyService
    {
        public int? GetTaxonRankId(int taxonId) => 22;
        public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId) => [];
        public List<int> GetDescendantSpeciesIds(int taxonId) => [];
        public List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId) => [];

        public List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds) =>
            taxonIds.Select(id => new TaxonAncestryDto
            {
                Id = id,
                // Ukjente taxa får tom kjede, slik den ekte tjenesten gjør
                ParentIds = Ancestors.TryGetValue(id, out var a) ? a : [],
            }).ToList();
    }

    private static readonly Hierarchy Sut = new();

    [Fact]
    public void Fjerner_etterkommer_naar_forfar_er_valgt()
    {
        Sut.RemoveRedundantDescendants([1, 1000]).Should().Equal(1);
    }

    [Fact]
    public void Fjerner_etterkommer_uavhengig_av_rekkefolge()
    {
        Sut.RemoveRedundantDescendants([1000, 1]).Should().Equal(1);
    }

    [Fact]
    public void Fjerner_alle_mellomnivaaer_under_valgt_forfar()
    {
        Sut.RemoveRedundantDescendants([1, 10, 100, 1000]).Should().Equal(1);
    }

    [Fact]
    public void Beholder_dypeste_forfar_naar_toppen_ikke_er_valgt()
    {
        Sut.RemoveRedundantDescendants([10, 100, 1000]).Should().Equal(10);
    }

    [Fact]
    public void Beholder_soesken_i_ulike_grener()
    {
        Sut.RemoveRedundantDescendants([1, 2]).Should().Equal(1, 2);
    }

    [Fact]
    public void Beholder_rekkefolgen_fra_inndata()
    {
        Sut.RemoveRedundantDescendants([2, 1]).Should().Equal(2, 1);
    }

    [Fact]
    public void Fjerner_duplikater()
    {
        Sut.RemoveRedundantDescendants([1000, 1000]).Should().Equal(1000);
    }

    [Fact]
    public void Beholder_ukjente_taxa()
    {
        // Et ukjent takson gir tomt resultat lenger nede uansett. Å fjerne det her
        // ville skjult feilen i stedet for å vise den.
        Sut.RemoveRedundantDescendants([1, 999999]).Should().Equal(1, 999999);
    }

    [Fact]
    public void Null_og_tomt_gaar_uendret_gjennom()
    {
        Sut.RemoveRedundantDescendants(null).Should().BeNull();
        Sut.RemoveRedundantDescendants([]).Should().BeEmpty();
    }

    [Fact]
    public void Ett_element_rorer_ikke_hierarkiet()
    {
        // Kortslutningen er en ytelsesdetalj, men den er også den vanligste stien:
        // de aller fleste utvalg er ett takson.
        Sut.RemoveRedundantDescendants([1000]).Should().Equal(1000);
    }
}

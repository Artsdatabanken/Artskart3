using System.Reflection;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tester for GetAncestries i TaxonHierarchyService.
/// Hierarkiet fylles direkte i de private in-memory-strukturene via refleksjon,
/// slik at vi slipper databasetilgang (LastAsync krever IArtsKartDbContext).
/// </summary>
public class TaxonHierarchyServiceTests
{
    // Hierarki: 1 (rot) -> 2 -> 3 -> 4, og 1 -> 5
    private static readonly Dictionary<int, (int ParentTaxonId, int TaxonRankId)> Hierarchy = new()
    {
        [1] = (0, 10),
        [2] = (1, 20),
        [3] = (2, 21),
        [4] = (3, 22),
        [5] = (1, 22),
    };

    private readonly TaxonHierarchyService _sut;

    public TaxonHierarchyServiceTests()
    {
        _sut = new TaxonHierarchyService(new Mock<IServiceScopeFactory>().Object, NullLogger<TaxonHierarchyService>.Instance);

        var serviceType = typeof(TaxonHierarchyService);
        var taxonDataType = serviceType.GetNestedType("TaxonData", BindingFlags.NonPublic)!;

        var taxonsField = serviceType.GetField("_taxons", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ranksField = serviceType.GetField("_taxonRanks", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var childrenField = serviceType.GetField("_childrenByParent", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var initializedField = serviceType.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var taxons = new Dictionary<int, object>();
        var ranks = new Dictionary<int, int>();
        var children = new Dictionary<int, List<int>>();

        foreach (var (id, (parentId, rankId)) in Hierarchy)
        {
            var data = Activator.CreateInstance(taxonDataType)!;
            taxonDataType.GetProperty("Id")!.SetValue(data, id);
            taxonDataType.GetProperty("ParentTaxonId")!.SetValue(data, parentId);
            taxonDataType.GetProperty("TaxonRankId")!.SetValue(data, rankId);
            taxonDataType.GetProperty("ExistsInCountry")!.SetValue(data, true);
            taxons[id] = data;
            ranks[id] = rankId;

            if (parentId == 0) continue;
            if (!children.TryGetValue(parentId, out var list))
            {
                list = [];
                children[parentId] = list;
            }
            list.Add(id);
        }

        // Sett inn i de typede dictionary-feltene via refleksjon
        var taxonsDict = (System.Collections.IDictionary)Activator.CreateInstance(taxonsField.FieldType)!;
        foreach (var (id, data) in taxons) taxonsDict[id] = data;
        var ranksDict = (System.Collections.IDictionary)Activator.CreateInstance(ranksField.FieldType)!;
        foreach (var (id, rank) in ranks) ranksDict[id] = rank;
        var childrenDict = (System.Collections.IDictionary)Activator.CreateInstance(childrenField.FieldType)!;
        foreach (var (parentId, list) in children) childrenDict[parentId] = list;

        taxonsField.SetValue(_sut, taxonsDict);
        ranksField.SetValue(_sut, ranksDict);
        childrenField.SetValue(_sut, childrenDict);
        initializedField.SetValue(_sut, true);
    }

    [Fact]
    public void GetAncestries_ReturnererTomKjede_ForRotnode()
    {
        var result = _sut.GetAncestries([1]);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
        result[0].ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void GetAncestries_ReturnererKjedeFraRot_ForDypNode()
    {
        var result = _sut.GetAncestries([4]);

        result[0].ParentIds.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GetAncestries_EkskludererSegSelv()
    {
        var result = _sut.GetAncestries([3]);

        result[0].ParentIds.Should().Equal(1, 2);
        result[0].ParentIds.Should().NotContain(3);
    }

    [Fact]
    public void GetAncestries_ReturnererTomKjede_ForUkjentId()
    {
        var result = _sut.GetAncestries([999]);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(999);
        result[0].ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void GetAncestries_BatcherFlereIderOgFjernerDuplikater()
    {
        var result = _sut.GetAncestries([4, 5, 4]);

        result.Should().HaveCount(2);
        result.Single(a => a.Id == 4).ParentIds.Should().Equal(1, 2, 3);
        result.Single(a => a.Id == 5).ParentIds.Should().Equal(1);
    }

    [Fact]
    public void GetAncestries_InkludererSynligeBarnPerNivaIKjeden()
    {
        var result = _sut.GetAncestries([4]);

        // Kjeden 1 -> 2 -> 3 -> 4: nivåer for 1, 2, 3 og 4 selv
        var levels = result[0].Levels;
        levels.Select(l => l.ParentId).Should().Equal(1, 2, 3, 4);
        levels.Single(l => l.ParentId == 1).ChildIds.Should().BeEquivalentTo([2, 5]);
        levels.Single(l => l.ParentId == 2).ChildIds.Should().Equal(3);
        levels.Single(l => l.ParentId == 3).ChildIds.Should().Equal(4);
        levels.Single(l => l.ParentId == 4).ChildIds.Should().BeEmpty();
    }

    [Fact]
    public void GetAncestries_HandtererSykliskData()
    {
        // Legg til en syklus: 6 <-> 7
        var serviceType = typeof(TaxonHierarchyService);
        var taxonDataType = serviceType.GetNestedType("TaxonData", BindingFlags.NonPublic)!;
        var taxonsField = serviceType.GetField("_taxons", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taxons = (System.Collections.IDictionary)taxonsField.GetValue(_sut)!;

        foreach (var (id, parentId) in new[] { (6, 7), (7, 6) })
        {
            var data = Activator.CreateInstance(taxonDataType)!;
            taxonDataType.GetProperty("Id")!.SetValue(data, id);
            taxonDataType.GetProperty("ParentTaxonId")!.SetValue(data, parentId);
            taxonDataType.GetProperty("TaxonRankId")!.SetValue(data, 10);
            taxons[id] = data;
        }

        var result = _sut.GetAncestries([6]);

        result[0].ParentIds.Should().Equal(7);
    }
}

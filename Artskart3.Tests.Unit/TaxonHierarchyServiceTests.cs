using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

public class TaxonHierarchyServiceTests
{
    // Animalia (1) -> Chordata (2) -> Aves (3) -> Paridae (4) -> Parus major (5) + Parus caeruleus (6)
    private const int AnimaliaId = 1;
    private const int ChordataId = 2;
    private const int AvesId = 3;
    private const int ParidaeId = 4;

    [Fact]
    public async Task GetChildren_ForRootNodes_ReturnsEmptyParents()
    {
        var sut = await CreateStartedServiceAsync();

        var result = sut.GetChildren(null);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(AnimaliaId);
        result[0].Parents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChildren_ForNestedTaxon_ReturnsFullParentChainFromRoot()
    {
        var sut = await CreateStartedServiceAsync();

        var result = sut.GetChildren(ParidaeId);

        result.Should().HaveCount(2);
        foreach (var node in result)
        {
            node.Parents.Select(p => p.Id)
                .Should().Equal(AnimaliaId, ChordataId, AvesId, ParidaeId);
        }
    }

    [Fact]
    public async Task GetChildren_ParentChain_IncludesNavnOgFlagg()
    {
        var sut = await CreateStartedServiceAsync();

        var result = sut.GetChildren(ParidaeId);

        var nearestParent = result[0].Parents.Last();
        nearestParent.Id.Should().Be(ParidaeId);
        nearestParent.ValidScientificName.Should().Be("Paridae");
        nearestParent.TaxonRankId.Should().Be(19);
        nearestParent.HasChildren.Should().BeTrue();
        nearestParent.Parents.Should().BeEmpty();
        nearestParent.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChildren_ParentChain_TarMedForeldreUtenObservasjoner()
    {
        // Aves har verken observasjoner eller ExistsInCountry, men skal likevel være med i kjeden
        var sut = await CreateStartedServiceAsync();

        var result = sut.GetChildren(ParidaeId);

        result[0].Parents.Should().Contain(p => p.Id == AvesId);
    }

    [Fact]
    public async Task GetChildren_EachNode_GetsItsOwnParentsList()
    {
        var sut = await CreateStartedServiceAsync();

        var result = sut.GetChildren(ParidaeId);

        result[0].Parents.Should().NotBeSameAs(result[1].Parents);
    }

    [Fact]
    public async Task GetChildren_MedSyklusIForeldrekjeden_AvbryterUtenUendeligLokke()
    {
        // 100 og 101 peker på hverandre — skal ikke henge
        var sut = await CreateStartedServiceAsync(
            CreateTaxon(100, parentTaxonId: 101, "Syklisk A", rankId: 10, observationCount: 5),
            CreateTaxon(101, parentTaxonId: 100, "Syklisk B", rankId: 11, observationCount: 5),
            CreateTaxon(102, parentTaxonId: 100, "Barn av A", rankId: 12, observationCount: 5));

        var result = sut.GetChildren(100);

        result.Should().ContainSingle(n => n.Id == 102);
        result.Single(n => n.Id == 102).Parents.Select(p => p.Id)
            .Should().Equal(101, 100);
    }

    [Fact]
    public async Task GetChildren_ForUkjentForelder_ReturnsEmpty()
    {
        var sut = await CreateStartedServiceAsync();

        sut.GetChildren(9999).Should().BeEmpty();
    }

    private static async Task<TaxonHierarchyService> CreateStartedServiceAsync(params Taxon[] extraTaxons)
    {
        var context = new ArtskartDbContext(
            new DbContextOptionsBuilder<ArtskartDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        context.Set<Taxon>().AddRange(
            CreateTaxon(AnimaliaId, parentTaxonId: 0, "Animalia", rankId: 4, observationCount: 100),
            CreateTaxon(ChordataId, parentTaxonId: AnimaliaId, "Chordata", rankId: 8, observationCount: 90),
            CreateTaxon(AvesId, parentTaxonId: ChordataId, "Aves", rankId: 14, observationCount: null),
            CreateTaxon(ParidaeId, parentTaxonId: AvesId, "Paridae", rankId: 19, observationCount: 50),
            CreateTaxon(5, parentTaxonId: ParidaeId, "Parus major", rankId: 22, observationCount: 30),
            CreateTaxon(6, parentTaxonId: ParidaeId, "Parus caeruleus", rankId: 22, observationCount: 20));
        context.Set<Taxon>().AddRange(extraTaxons);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped<IArtsKartDbContext>(_ => context);

        var sut = new TaxonHierarchyService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TaxonHierarchyService>.Instance);

        await sut.StartAsync(CancellationToken.None);
        return sut;
    }

    private static Taxon CreateTaxon(int id, int parentTaxonId, string name, int rankId, int? observationCount) =>
        new()
        {
            Id = id,
            ParentTaxonId = parentTaxonId,
            ValidScientificName = name,
            PreferredPopularName = name,
            TaxonRankId = rankId,
            TaxonGroupId = 1,
            CumulativeObservationCount = observationCount,
            ExistsInCountry = false,
            ScientificNameIdHiarchy = string.Empty,
            TaxonIdHiarchy = string.Empty
        };
}

using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Tests.Unit;

public class ObservationRepositoryTests
{
    [Fact]
    public async Task GetObservationDetails_WithMatchingLocationAndObservation_ReturnsMappedDetails()
    {
        await using var context = CreateInMemoryContext();
        var taxon = CreateTaxon(1, "<i>Parus major</i>", "Great tit");
        var matchingObservation = CreateObservation(42, 10, taxon);
        var otherLocationObservation = CreateObservation(43, 20, taxon);
        otherLocationObservation.MatchedScientificName = matchingObservation.MatchedScientificName;
        context.Set<Taxon>().Add(taxon);
        context.Set<Observation>().AddRange(matchingObservation, otherLocationObservation);
        await context.SaveChangesAsync();
        var sut = CreateRepository(context);

        var result = await sut.GetObservationDetails(10, 42);

        result.Id.Should().Be(42);
        result.PreferredPopularName.Should().Be("Great tit");
        result.ScientificName.Should().Be("<i>Parus major</i>");
        result.Author.Should().Be("Linnaeus");
    }

    [Fact]
    public async Task GetObservationDetails_WithNoMatchingPair_Throws()
    {
        await using var context = CreateInMemoryContext();
        var taxon = CreateTaxon(1, "Parus major", "Great tit");
        context.Set<Taxon>().Add(taxon);
        context.Set<Observation>().Add(CreateObservation(42, 10, taxon));
        await context.SaveChangesAsync();
        var sut = CreateRepository(context);

        var act = () => sut.GetObservationDetails(20, 42);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetObservationByLocations_FiltersAndMapsObservationFields()
    {
        await using var context = CreateInMemoryContext();
        var taxon = CreateTaxon(1, "<i>Parus major</i>", "Great tit");
        var fallbackTaxon = CreateTaxon(2, "<i>Corvus corax</i>", null);
        var taxonGroup = new TaxonGroup { Id = 7, Name = "Birds" };
        var category = new Category { Id = 8, Code = "A", Name = "Validated", CategoryTypeId = 1 };
        var tag = new Tag { Id = 9, Name = "Visible" };
        taxon.TaxonGroup = taxonGroup;
        fallbackTaxon.TaxonGroup = taxonGroup;
        context.Set<Taxon>().AddRange(taxon, fallbackTaxon);
        context.Set<TaxonGroup>().Add(taxonGroup);
        context.Set<Category>().Add(category);
        context.Set<Tag>().Add(tag);

        var included = CreateObservation(42, 10, taxon);
        included.TaxonGroupId = taxonGroup.Id;
        included.CategoryId = category.Id;
        included.Category = category;
        included.Tags.Add(tag);
        included.ObservationDetail = new ObservationDetail
        {
            Id = included.Id,
            AssociatedReferences = string.Empty,
            Collector = "Ada Lovelace"
        };

        var fallback = CreateObservation(43, 10, fallbackTaxon);
        fallback.TaxonGroupId = taxonGroup.Id;
        fallback.CategoryId = null;
        fallback.Category = null;

        var excluded = CreateObservation(44, 20, taxon);
        excluded.TaxonGroupId = taxonGroup.Id;
        excluded.MatchedScientificName = included.MatchedScientificName;
        context.Set<Observation>().AddRange(included, fallback, excluded);
        await context.SaveChangesAsync();
        var sut = CreateRepository(context);

        var result = (await sut.GetObservationByLocations([10])).ToList();

        result.Should().HaveCount(2);
        var mapped = result.Single(observation => observation.Id == 42);
        mapped.PreferredPopularName.Should().Be("Great tit");
        mapped.ScientificName.Should().Be("<i>Parus major</i>");
        mapped.DisplayName.Should().Be("Great tit");
        mapped.Author.Should().Be("Linnaeus");
        mapped.TaxonGroupId.Should().Be(7);
        mapped.TaxonGroupName.Should().Be("Birds");
        mapped.CategoryId.Should().Be(8);
        mapped.CategoryName.Should().Be("Validated");
        mapped.LocationId.Should().Be(10);
        mapped.RegistrationType.Should().ContainSingle().Which.Should().Be("Visible");
        mapped.Collector.Should().Be("Ada Lovelace");

        var fallbackResult = result.Single(observation => observation.Id == 43);
        fallbackResult.DisplayName.Should().Be("Corvus corax");
        fallbackResult.CategoryName.Should().BeEmpty();
        fallbackResult.Collector.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationByLocations_WithEmptyLocationIds_ReturnsEmpty()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var result = await sut.GetObservationByLocations([]);

        result.Should().BeEmpty();
    }

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static ObservationRepository CreateRepository(ArtskartDbContext context) =>
        new(context);

    private static Taxon CreateTaxon(int id, string scientificName, string? popularName) =>
        new()
        {
            Id = id,
            ValidScientificName = scientificName,
            ValidScientificNameAuthorship = "Linnaeus",
            PreferredPopularName = popularName,
            TaxonGroupId = 7,
            ScientificNameIdHiarchy = id.ToString(),
            TaxonIdHiarchy = id.ToString()
        };

    private static Observation CreateObservation(int id, int locationId, Taxon taxon) =>
        new()
        {
            Id = id,
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            BasisOfRecordId = 1,
            TaxonId = taxon.Id,
            MatchedScientificNameId = taxon.Id,
            TaxonGroupId = taxon.TaxonGroupId,
            Latitude = 59.9,
            Longitude = 10.7,
            LocationId = locationId,
            HashCode = id,
            ProcessEngineId = 1,
            HasAnnotations = false,
            HasErrors = false,
            Taxon = taxon,
            MatchedScientificName = new TaxonName { Id = taxon.Id, ScientificName = taxon.ValidScientificName! }
        };
}

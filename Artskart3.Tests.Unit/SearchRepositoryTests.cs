using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Constants;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NetTopologySuite.IO;
using RegistrationStatus = Artskart3.Core.Domain.Enums.RegistrationStatus;
using TagEnum = Artskart3.Core.Domain.Enums.Tag;
using LocationGeometryType = Artskart3.Core.Domain.Enums.LocationGeometryType;

namespace Artskart3.Tests.Unit;

public class SearchRepositoryTests
{
    [Fact]
    public async Task GetTaxonsAsync_WithNullName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

        var result = await sut.GetTaxonsAsync(null!);

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithWhitespaceName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

        var result = await sut.GetTaxonsAsync("   ");

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountZero_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

        var act = () => sut.GetTaxonsAsync("fugl", 0);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountTooHigh_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

        var act = () => sut.GetTaxonsAsync("fugl", 1001);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithNegativeMaxCount_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

        var act = () => sut.GetTaxonsAsync("fugl", -1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetLocationsAsync_WithNullFilter_UsesDefaultsAndReturnsLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 1),
            CreateObservation(3, 2));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(null);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithTaxonGroupIdFilter_ReturnsOnlyMatchingLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        SeedObservations(context,
            CreateObservation(1, 1, taxonGroupId: 1),
            CreateObservation(2, 2, taxonGroupId: 2));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 1 } });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithCategoryFilter_ReturnsOnlyMatchingLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        SeedObservations(context,
            CreateObservation(1, 1, categoryId: 10),
            CreateObservation(2, 2, categoryId: 20));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { CategoryIds = new[] { 10 } });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithBasisOfRecordFilter_ReturnsOnlyMatchingLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        SeedObservations(context,
            CreateObservation(1, 1, basisOfRecordId: 5),
            CreateObservation(2, 2, basisOfRecordId: 8));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { BasisOfRecordIds = new[] { 5 } });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithRegistrationStatusPresent_ExcludesAbsentAndNotRecovered()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"),
            CreateLocation(4, "Tromso"));

        var absentTag = CreateTag(5, "Absent");
        var notRecoveredTag = CreateTag(6, "NotRecovered");
        var validatedTag = CreateTag(3, "Validated");
        context.Set<Tag>().AddRange(absentTag, notRecoveredTag, validatedTag);

        var foundObservation = CreateObservation(1, 1);
        var absentObservation = CreateObservation(2, 2);
        absentObservation.Tags.Add(absentTag);
        absentObservation.RegistrationStatusId = 2;
        var notRecoveredObservation = CreateObservation(3, 3);
        notRecoveredObservation.Tags.Add(notRecoveredTag);
        notRecoveredObservation.RegistrationStatusId = 3;
        var otherTagObservation = CreateObservation(4, 4);
        otherTagObservation.Tags.Add(validatedTag);
        // Validated er ikke en registreringsstatus - observasjonen er fortsatt «funnet».
        otherTagObservation.RegistrationStatusId = 1;

        SeedObservations(context,
            foundObservation,
            absentObservation,
            notRecoveredObservation,
            otherTagObservation);

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { RegistrationStatusId = 1 });

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 4]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithRegistrationStatusAbsent_ReturnsOnlyAbsent()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        var absentTag = CreateTag(5, "Absent");
        context.Set<Tag>().Add(absentTag);

        var absentObservation = CreateObservation(1, 1);
        absentObservation.Tags.Add(absentTag);
        absentObservation.RegistrationStatusId = 2;
        var foundObservation = CreateObservation(2, 2);

        SeedObservations(context, absentObservation, foundObservation);
        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { RegistrationStatusId = 2 });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithRegistrationStatusNotRefound_ReturnsOnlyNotRecovered()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        var notRecoveredTag = CreateTag(6, "NotRecovered");
        context.Set<Tag>().Add(notRecoveredTag);

        var notRecoveredObservation = CreateObservation(1, 1);
        notRecoveredObservation.Tags.Add(notRecoveredTag);
        notRecoveredObservation.RegistrationStatusId = 3;
        var foundObservation = CreateObservation(2, 2);

        SeedObservations(context, notRecoveredObservation, foundObservation);
        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { RegistrationStatusId = 3 });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_SetsRegistrationStatusFromAllObservationsAtLocation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Funn og ikke funnet"),
            CreateLocation(2, "Ikke funnet"),
            CreateLocation(3, "Ikke funnet og ikke gjenfunnet"),
            CreateLocation(4, "Begge tagger"),
            CreateLocation(5, "Annen tagg"));

        var absentTag = CreateTag(5, "Absent");
        var notRecoveredTag = CreateTag(6, "NotRecovered");
        var validatedTag = CreateTag(3, "Validated");
        context.Set<Tag>().AddRange(absentTag, notRecoveredTag, validatedTag);

        Observation Tagged(int id, int locationId, params Tag[] tags)
        {
            var observation = CreateObservation(id, locationId);
            foreach (var tag in tags) observation.Tags.Add(tag);
            return observation;
        }

        SeedObservations(context,
            Tagged(1, 1),
            Tagged(2, 1, absentTag),
            Tagged(3, 2, absentTag),
            Tagged(4, 2, absentTag),
            Tagged(5, 3, absentTag),
            Tagged(6, 3, notRecoveredTag),
            Tagged(7, 4, absentTag, notRecoveredTag),
            Tagged(8, 5, validatedTag));
        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(null);

        result.ToDictionary(x => x.Id, x => x.RegistrationStatus).Should().BeEquivalentTo(new Dictionary<int, RegistrationStatus>
        {
            [1] = RegistrationStatus.Found,
            [2] = RegistrationStatus.Absent,
            [3] = RegistrationStatus.NotRecovered,
            [4] = RegistrationStatus.NotRecovered,
            [5] = RegistrationStatus.Found,
        });
    }

    [Fact]
    public async Task GetLocationsAsync_RegistrationStatusIgnoresObservationsOutsideFilter()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo"));

        var absentTag = CreateTag(5, "Absent");
        context.Set<Tag>().Add(absentTag);

        var absentObservation = CreateObservation(1, 1, taxonGroupId: 1);
        absentObservation.Tags.Add(absentTag);
        SeedObservations(context, absentObservation, CreateObservation(2, 1, taxonGroupId: 2));
        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = [1] });

        result.Should().ContainSingle().Which.RegistrationStatus.Should().Be(RegistrationStatus.Absent);
    }

    // CollectionId-filtrering er erstattet av OrganizationIds via ObservationEntityIndex.
    // Denne testen krever seeding av ObservationEntityIndex og dekkes av integrasjonstester.

    [Fact]
    public async Task GetLocationsAsync_WithCoordinatePrecisionFromFilter_FiltersCorrectly()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1, coordinatePrecisionInMeters: 10),
            CreateObservation(2, 2, coordinatePrecisionInMeters: 50),
            CreateObservation(3, 3, coordinatePrecisionInMeters: 100));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 50 } });

        result.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithCoordinatePrecisionToFilter_FiltersCorrectly()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1, coordinatePrecisionInMeters: 10),
            CreateObservation(2, 2, coordinatePrecisionInMeters: 50),
            CreateObservation(3, 3, coordinatePrecisionInMeters: 100));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { To = 50 } });

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithBothPrecisionFilters_FiltersCorrectly()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1, coordinatePrecisionInMeters: 10),
            CreateObservation(2, 2, coordinatePrecisionInMeters: 50),
            CreateObservation(3, 3, coordinatePrecisionInMeters: 100));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 25, To = 75 }
        });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetLocationsAsync_WithMaxResults_LimitsResults()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 1),
            CreateObservation(3, 1),
            CreateObservation(4, 2),
            CreateObservation(5, 2),
            CreateObservation(6, 3));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = 2 });

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenMaxResultsExceedsMax_FallsBackToDefault100000()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var locations = Enumerable.Range(1, 1000)
            .Select(id => CreateLocation(id, $"Lokalitet {id}"))
            .ToArray();
        var observations = Enumerable.Range(1, 1000)
            .Select(id => CreateObservation(id, id))
            .ToArray();

        SeedLocations(context, locations);
        SeedObservations(context, observations);

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = SearchConstants.MaxLocationResults + 1 });

        result.Should().HaveCount(1000);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenNoObservationsMatchFilter_ReturnsEmpty()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo"));
        SeedObservations(context, CreateObservation(1, 1, taxonGroupId: 1));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 99 } });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationsAsync_WithInvalidCommaListValues_IgnoresInvalidEntries()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"),
            CreateLocation(3, "Bergen"));

        SeedObservations(context,
            CreateObservation(1, 1, taxonGroupId: 1),
            CreateObservation(2, 2, taxonGroupId: 2),
            CreateObservation(3, 3, taxonGroupId: 3));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 1, 2 } });

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task GetLocationsAsync_WithEmptyStringFilter_IgnoresFilter()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        SeedObservations(context,
            CreateObservation(1, 1, taxonGroupId: 1),
            CreateObservation(2, 2, taxonGroupId: 2));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = null });

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLocationsAsync_GroupsByLocationId_AggregatatesObservationCount()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo"));
        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 1),
            CreateObservation(3, 1));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLocationsAsync_MultipleTaxaAtSameLocation_ReturnsSingleLocationWithTotalCount()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo"));
        SeedObservations(context,
            CreateObservation(1, 1, taxonId: 10),
            CreateObservation(2, 1, taxonId: 10),
            CreateObservation(3, 1, taxonId: 20),
            CreateObservation(4, 1, taxonId: 30));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(4);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenLocationRowMissing_OmitsFromResults()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo"));
        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 999));

        await context.SaveChangesAsync();

        var result = await sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationListInfo_WithValidRequest_ReturnsObservations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var location = CreateLocation(5000, "Test Locality");
        SeedLocations(context, location);
        context.Set<TaxonGroup>().Add(new TaxonGroup { Id = 1, Name = "Test group" });
        context.Set<Taxon>().Add(CreateTaxon(1, "Test species", "Testus species"));
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Testus species" });
        SeedObservations(context,
            CreateObservation(1, 5000),  // locationId = 5000
            CreateObservation(2, 5000),  // locationId = 5000
            CreateObservation(3, 5000)); // locationId = 5000

        await context.SaveChangesAsync();

        var request = new ObservationListInfoRequestDto { Ids = [5000], Filter = null };
        var result = (await sut.GetObservationListInfo(request)).ToList();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetObservationListInfo_ReturnsAllMatchesAndStoredMetadata()
    {
        await using var context = CreateInMemoryContext();
        var location = CreateLocation(5000, "Test Locality");
        location.Areas.Add(CreateArea(1, "Municipality", 1, null));
        location.Areas.Add(CreateArea(2, "County", 2, null));
        var historicalArea = CreateArea(3, "Old municipality", 1, null);
        historicalArea.IsCurrent = false;
        location.Areas.Add(historicalArea);
        SeedLocations(context, location);
        context.Set<TaxonGroup>().Add(new TaxonGroup { Id = 1, Name = "Test group" });
        context.Set<Taxon>().Add(CreateTaxon(1, "Test species", "Testus species"));
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Testus species" });
        context.Set<Category>().Add(new Category { Id = 1, Code = "NE", Name = "Ikke vurdert", CategoryTypeId = 1 });
        var collected = new DateTime(2024, 10, 5);
        var observations = Enumerable.Range(1, 2501).Select(id =>
        {
            var observation = CreateObservation(id, 5000);
            observation.DateTimeCollected = collected;
            return observation;
        }).ToArray();
        observations[0].ObservationDetail = new ObservationDetail { Id = 1, Collector = "Collector", AssociatedReferences = "" };
        SeedObservations(context, observations);
        await context.SaveChangesAsync();

        var result = (await CreateRepository(context).GetObservationListInfo(new() { Ids = [5000] })).ToList();

        result.Should().HaveCount(2501);
        result.Select(o => o.Id).Should().Equal(Enumerable.Range(1, 2501).Reverse());
        result.Should().OnlyContain(o => o.TaxonId == 1 && o.DateTimeCollected == collected &&
            o.Locality == "Test Locality" && o.MunicipalityName == "Municipality" && o.CountyName == "County" &&
            o.CategoryCode == "NE" && o.CategoryTypeId == 1);
        result.Last().Collector.Should().Be("Collector");
    }

    [Fact]
    public async Task GetObservationListInfo_DoesNotChooseAnAmbiguousMunicipality()
    {
        await using var context = CreateInMemoryContext();
        var location = CreateLocation(5000, "Boundary");
        location.Areas.Add(CreateArea(1, "First", 1, null));
        location.Areas.Add(CreateArea(2, "Second", 1, null));
        SeedLocations(context, location);
        context.Set<TaxonGroup>().Add(new TaxonGroup { Id = 1, Name = "Test group" });
        context.Set<Taxon>().Add(CreateTaxon(1, "Test species", "Testus species"));
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Testus species" });
        context.Set<Category>().Add(new Category { Id = 3, Code = "NR", Name = "Ikke risikovurdert", CategoryTypeId = 2 });
        SeedObservations(context, CreateObservation(1, 5000, categoryId: 3), CreateObservation(2, 5000, categoryId: null));
        await context.SaveChangesAsync();

        var result = (await CreateRepository(context).GetObservationListInfo(new() { Ids = [5000, 5000] })).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(o => o.MunicipalityName == null && o.CountyName == null);
        result.Single(o => o.Id == 1).CategoryCode.Should().Be("NR");
        result.Single(o => o.Id == 2).CategoryCode.Should().BeNull();
    }

    [Fact]
    private async Task GetObservationListInfo_ReturnsEmptyRequestIds()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var location = CreateLocation(5000, "Test Locality");
        SeedLocations(context, location);
        context.Set<TaxonGroup>().Add(new TaxonGroup { Id = 1, Name = "Test group" });
        context.Set<Taxon>().Add(CreateTaxon(1, "Test species", "Testus species"));
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Testus species" });
        SeedObservations(context,
            CreateObservation(1, 5000),  // locationId = 5000
            CreateObservation(2, 5000),  // locationId = 5000
            CreateObservation(3, 5000)); // locationId = 5000

        await context.SaveChangesAsync();

        var request = new ObservationListInfoRequestDto { Ids = [], Filter = null };
        var result = (await sut.GetObservationListInfo(request)).ToList();

        result.Should().HaveCount(0);
    }

    [Fact]
    private async Task GetObservationListInfo_ReturnsFilteredRegistrations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var location = CreateLocation(5000, "Test Locality");
        SeedLocations(context, location);
        context.Set<TaxonGroup>().Add(new TaxonGroup { Id = 8, Name = "Fugler" });
        context.Set<Taxon>().Add(CreateTaxon(1, "Test species", "Testus species", taxonGroupId: 8));
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Testus species" });
        SeedObservations(context,
            CreateObservation(1, 5000, 8),  // locationId = 5000
            CreateObservation(2, 5000, 8),  // locationId = 5000
            CreateObservation(3, 5000, 2)); // locationId = 5000

        await context.SaveChangesAsync();

        var request = new ObservationListInfoRequestDto { Ids = [5000], Filter = new ObservationSearchFilterDto { TaxonGroupIds = [8]} };
        var result = (await sut.GetObservationListInfo(request)).ToList();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_SkipsGridSquares_ExceptSensitiveMaskingSquares()
    {
        await using var context = CreateInMemoryContext();
        var wktReader = new WKTReader();
        const string Square = "POLYGON ((0 0, 4000 0, 4000 4000, 0 4000, 0 0))";

        var gridSquare = CreateLocation(1, "Rute");
        gridSquare.Geometry = wktReader.Read(Square);
        var sensitiveSquare = CreateLocation(2, "Locality data withheld");
        sensitiveSquare.LookupId = "1_2000_2000_4_sensitiv";
        sensitiveSquare.Geometry = wktReader.Read(Square);
        var irregularPolygon = CreateLocation(3, "Myr");
        irregularPolygon.Geometry = wktReader.Read("POLYGON ((0 0, 4000 0, 4000 2000, 2000 4000, 0 4000, 0 0))");
        SeedLocations(context, gridSquare, sensitiveSquare, irregularPolygon);
        SeedObservations(context, CreateObservation(1, 1), CreateObservation(2, 2), CreateObservation(3, 3));
        await context.SaveChangesAsync();

        var result = await CreateRepository(context).GetLocationPolygonsAsync();

        result.Select(p => p.LocationId).Should().BeEquivalentTo([2, 3]);
    }

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static SearchRepository CreateRepository(ArtskartDbContext context) =>
        new(context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

    /// <summary>
    /// Legger inn lokasjonene OG setter GeometryTypeId, som databasen eier.
    ///
    /// Kolonnen er beregnet og persistert fra Geometry, så entiteten har privat
    /// setter og InMemory-provideren fyller den aldri. Polygonstien filtrerer på
    /// nettopp den kolonnen — ikke på STGeometryType() — så uten dette ville
    /// testene sett en tom database. Seederen spiller her samme rolle for
    /// kolonnen som SeedObservations gjør for backfillen.
    /// </summary>
    private static void SeedLocations(ArtskartDbContext context, params Location[] locations)
    {
        context.Set<Location>().AddRange(locations);

        foreach (var location in locations)
        {
            context.Entry(location).Property(l => l.GeometryTypeId).CurrentValue =
                GeometritypeFor(location.Geometry);
        }
    }

    /// <summary>
    /// Samme avbildning som den beregnede kolonnen i ArtskartDbContext. Endres den
    /// ene, må den andre følge.
    /// </summary>
    private static LocationGeometryType GeometritypeFor(NetTopologySuite.Geometries.Geometry? geometry) =>
        geometry?.GeometryType switch
        {
            "Point" => LocationGeometryType.Point,
            "Polygon" => LocationGeometryType.Polygon,
            "MultiPolygon" => LocationGeometryType.MultiPolygon,
            "LineString" => LocationGeometryType.LineString,
            "MultiLineString" => LocationGeometryType.MultiLineString,
            "GeometryCollection" => LocationGeometryType.GeometryCollection,
            _ => LocationGeometryType.Ukjent,
        };

    /// <summary>
    /// Legger inn observasjonene OG den tilhørende raden i ObservationEntityIndex.
    ///
    /// Lokasjonssøket leser fra indekstabellen, ikke fra Observation, fordi
    /// aggregeringen da kan gå i batch mode mot columnstore. Uten en indeksrad
    /// per observasjon ville testene her sett en tom database.
    ///
    /// Kolonnene kopieres fra observasjonen, slik seksjon C, D og E i BackfillAll
    /// gjør i produksjon. Det gjør seederen til en levende beskrivelse av den
    /// invarianten: leser repositoriet en kolonne som backfillen ikke fyller,
    /// feiler testene her.
    ///
    /// Én rad per observasjon, av typen Municipality. Ekte data har én rad per
    /// område observasjonen ligger i — derfor teller repositoriet distinkte
    /// ObservationId og ikke rader. Områdefiltre dekkes av integrasjonstestene,
    /// som har ekte indeksdata.
    /// </summary>
    private static void SeedObservations(ArtskartDbContext context, params Observation[] observations)
    {
        context.Set<Observation>().AddRange(observations);

        var indexRows = observations.Select(o => new ObservationEntityIndex
        {
            ObservationId = o.Id,
            EntityTypeId = (int)Artskart3.Core.Domain.Enums.ObservationIndexEntityType.Municipality,
            EntityId = 1,
            LocationId = o.LocationId,
            TaxonGroupId = o.TaxonGroupId,
            CategoryId = o.CategoryId,
            BasisOfRecordId = o.BasisOfRecordId,
            CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters,
            DateTimeCollected = o.DateTimeCollected,
            RegistrationStatusId = RegistreringsstatusFor(o),
            HasMediaFiles = o.HasMediaFiles,
            BehaviorId = o.BehaviorId,
            InstitutionOrgId = o.InstitutionOrgId,
            DatasetOrgId = o.DatasetOrgId
        });

        context.Set<ObservationEntityIndex>().AddRange(indexRows);
    }

    /// <summary>
    /// Registreringsstatus utledes fra taggene, slik seksjon C i BackfillAll gjør:
    /// ikke gjenfunnet slår ikke funnet, og uten noen av dem står det brukeren
    /// satte på observasjonen (databasens DEFAULT er 1, funnet).
    ///
    /// Repositoriet leser den denormaliserte kolonnen og ikke taggene, så en test
    /// som bare legger på tagger ville ellers sett alt som funnet.
    /// </summary>
    private static byte RegistreringsstatusFor(Observation o) =>
        o.Tags.Any(t => t.Id == (int)TagEnum.NotRecovered) ? (byte)RegistrationStatus.NotRecovered
        : o.Tags.Any(t => t.Id == (int)TagEnum.Absent) ? (byte)RegistrationStatus.Absent
        : o.RegistrationStatusId;

    private static Location CreateLocation(int id, string locality) =>
        new()
        {
            Id = id,
            Locality = locality,
            Latitude = 59.91 + id,
            Longitude = 10.75 + id,
            East = 1000 + id,
            North = 2000 + id,
            CoordinatePrecision = 25,
            NodeId = 1,
            TimeStamp = DateTime.UtcNow,
            Geometry = null
        };

    private static Observation CreateObservation(
        int id,
        int locationId,
        int taxonGroupId = 1,
        int? categoryId = 1,
        int basisOfRecordId = 1,
        int institutionOrgId = 1,
        int? coordinatePrecisionInMeters = 25,
        int taxonId = 1) =>
        new()
        {
            Id = id,
            // Speiler databasens DEFAULT (1) paa kolonnen: 99,67 % av radene er
            // «funnet». Uten dette ville hver test maattet sette den selv.
            RegistrationStatusId = 1,
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            BasisOfRecordId = basisOfRecordId,
            TaxonId = taxonId,
            MatchedScientificNameId = 1,
            TaxonGroupId = taxonGroupId,
            CategoryId = categoryId,
            Latitude = 59.91,
            Longitude = 10.75,
            CoordinatePrecisionInMeters = coordinatePrecisionInMeters,
            East = 1000,
            North = 2000,
            LocationId = locationId,
            InstitutionOrgId = institutionOrgId,
            HashCode = id,
            ProcessEngineId = 1,
            HasAnnotations = false,
            HasErrors = false
        };

    private static Tag CreateTag(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Area CreateArea(int id, string name, int areaTypeId, int? observationCount, object? wktPolygon = null) =>
        new()
        {
            Id = id,
            DocumentId = $"doc-{id}",
            Fid = $"fid-{id}",
            Name = name,
            AreaTypeId = areaTypeId,
            ParentFid = "parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = observationCount,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
            WktPolygon = null,
            Centroid = null
        };
    private static Taxon CreateTaxon(int id, string preferredPopularName, string validScientificName, int taxonGroupId = 1, int taxonRankId = 1) =>
        new()
        {
            Id = id,
            PreferredPopularName = preferredPopularName,
            ValidScientificName = validScientificName,
            ValidScientificNameAuthorship = null,
            TaxonGroupId = taxonGroupId,
            TaxonRankId = taxonRankId,
            // satisfy non-nullable model properties
            ScientificNameIdHiarchy = string.Empty,
            TaxonIdHiarchy = string.Empty
        };

    /// <summary>
    /// Valget mellom grenformen og ELLER-formen. Grenformen henter TOP(skip + take)
    /// PER gren og fletter i minnet, så det er produktet som må begrenses — ikke
    /// grenantallet alene, som er det eneste taket som fantes.
    ///
    /// Regelen brukes av BEGGE grenformene: områdegrenene (opptil
    /// MaxAreaBranches = 64) og organisasjons-/taksongruppegrenene (opptil
    /// MaxBranches = 8). Den siste manglet kallet en periode, og casene merket
    /// «organisasjonsgrenene» under er det verste den stien slipper gjennom.
    /// </summary>
    [Theory]
    // Normale brukerreiser: side 1 med 10 per side gir take 40 med lookahead.
    [InlineData(1, 40, false)]
    [InlineData(64, 40, false)]
    // Grenantallet alene over taket.
    [InlineData(65, 1, true)]
    [InlineData(1500, 1, true)]
    // Lovlig grenantall, men dypt offset: 64 x (100 000 + 40 000) = 8,96 mill. rader.
    [InlineData(64, 140_000, true)]
    // Få grener, men dypt nok offset til å sprenge budsjettet alene.
    [InlineData(4, 140_000, true)]
    // Nøyaktig på budsjettet er innenfor; én rad over er utenfor.
    [InlineData(50, 10_000, false)]
    [InlineData(50, 10_001, true)]
    // Organisasjonsgrenene, verste lovlige kall: 8 organisasjoner, side 11 à 10 000
    // per side. MaxObservationSkip er 100 000 og MaxObservationResults 10 000, så
    // trengs blir 110 000 og produktet 880 000 rader — 1,76 x budsjettet.
    [InlineData(8, 110_000, true)]
    // Og den normale reisen gjennom samme sti skal fortsatt bruke grenformen.
    [InlineData(8, 50, false)]
    public void BrukEllerFormen_BegrenserBaadeGrenerOgProdukt(int grener, int trengs, bool forventet) =>
        SearchRepository.BrukEllerFormen(grener, trengs).Should().Be(forventet);

    /// <summary>
    /// Produktet må regnes i long. I int-aritmetikk ville 3 x 1 000 000 000 rent
    /// over til et negativt tall, sammenligningen blitt falsk, og den dyreste
    /// forespørselen av alle sluppet gjennom til grenformen.
    /// </summary>
    [Fact]
    public void BrukEllerFormen_RennerIkkeOverVedStoreVerdier() =>
        SearchRepository.BrukEllerFormen(3, 1_000_000_000).Should().BeTrue();

    public static TheoryData<LocationSearchFilterDto, bool> MatrikkelfiltreOgRecompile => new()
    {
        // Bare geografi: planen blir den samme uansett verdier.
        { new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"] }, false },
        { new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], MunicipalityIds = ["4206"] }, false },
        { new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], RestrictedAreaIds = ["Naturbase VV00000575"] }, false },
        // Med et filter som varierer i bredde: planen fra ett kan ikke gjenbrukes for et annet.
        { new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], TaxonGroupIds = [8] }, true },
        { new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], Sensitive = true }, true },
        // Uten matrikkelenhet gjelder ikke regelen.
        { new LocationSearchFilterDto { TaxonGroupIds = [8] }, false },
    };

    [Theory]
    [MemberData(nameof(MatrikkelfiltreOgRecompile))]
    public void NeedsRecompileWithCadastralUnits_BareMedAndreFiltreEnnGeografi(LocationSearchFilterDto filter, bool forventet) =>
        SearchRepository.NeedsRecompileWithCadastralUnits(filter).Should().Be(forventet);

    [Fact]
    public void NeedsRecompileWithCadastralUnits_EndrerIkkeFilteret()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], RestrictedAreaIds = ["Naturbase VV00000575"] };

        SearchRepository.NeedsRecompileWithCadastralUnits(filter);

        filter.CadastralUnitIds.Should().NotBeNull();
        filter.RestrictedAreaIds.Should().NotBeNull();
    }
}

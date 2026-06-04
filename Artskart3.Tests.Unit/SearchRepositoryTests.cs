using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchRepositoryTests
{
    [Fact]
    public async Task GetTaxonsAsync_WithNullName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

        var result = await sut.GetTaxonsAsync(null!);

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithWhitespaceName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

        var result = await sut.GetTaxonsAsync("   ");

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountZero_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

        var act = () => sut.GetTaxonsAsync("fugl", 0);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountTooHigh_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

        var act = () => sut.GetTaxonsAsync("fugl", 1001);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithNegativeMaxCount_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

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

        var result = await ToListAsync(sut.GetLocationsAsync(null));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = "1" }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { Categories = "10" }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { BasisOfRecords = "5" }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithCollectionIdFilter_ReturnsOnlyMatchingLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocation(1, "Oslo"),
            CreateLocation(2, "Trondheim"));

        SeedObservations(context,
            CreateObservation(1, 1, institutionCode: "NHM"),
            CreateObservation(2, 2, institutionCode: "GBIF"));

        await context.SaveChangesAsync();

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CollectionIds = "NHM" }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecisionFrom = 50 }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecisionTo = 50 }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto
        {
            CoordinatePrecisionFrom = 25,
            CoordinatePrecisionTo = 75
        }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = 2 }));

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenMaxResultsExceedsMax_FallsBackToDefault1000()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var locations = Enumerable.Range(1, 1105)
            .Select(id => CreateLocation(id, $"Lokalitet {id}"))
            .ToArray();
        var observations = Enumerable.Range(1, 1105)
            .Select(id => CreateObservation(id, id))
            .ToArray();

        SeedLocations(context, locations);
        SeedObservations(context, observations);

        await context.SaveChangesAsync();

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = 99999 }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = "99" }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = "1,abc,2" }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = string.Empty }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto()));

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(3);
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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto()));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_WithNoIds_ReturnsEmpty()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var result = await sut.GetAreasByTypeIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_WithAreaTypeId_ReturnsMatchingAreas()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Area>().AddRange(
            CreateArea(1, "Oslo", 1, 10),
            CreateArea(2, "Vestland", 2, 20));
        await context.SaveChangesAsync();

        var result = (await sut.GetAreasByTypeIdsAsync([1])).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Oslo");
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_GroupsByName_SumsObservationCounts()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Area>().AddRange(
            CreateArea(1, "Oslo", 1, 10),
            CreateArea(2, "Oslo", 1, 15));
        await context.SaveChangesAsync();

        var result = (await sut.GetAreasByTypeIdsAsync([1])).ToList();

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(25);
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_WithNullGeometry_HasNullCentroid()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Area>().Add(CreateArea(1, "Oslo", 1, 10, wktPolygon: null));
        await context.SaveChangesAsync();

        var result = (await sut.GetAreasByTypeIdsAsync([1])).ToList();

        result.Should().ContainSingle();
        result[0].Centroid.Should().BeNull();
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_SameNameDifferentAreaType_MergesIntoOneEntry()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Area>().AddRange(
            CreateArea(1, "Felles navn", 1, 10),
            CreateArea(2, "Felles navn", 2, 20));
        await context.SaveChangesAsync();

        var result = (await sut.GetAreasByTypeIdsAsync([1, 2])).ToList();

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(30);
    }

    [Fact]
    public async Task GetObservationsAsync_WithMunicipalityIdsFilter_ReturnsObservationInMatchingMunicipality()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var municipalityArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "0301", Name = "Oslo",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.Municipality,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(municipalityArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().Add(municipalityArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { MunicipalityIds = ["0301"] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithMunicipalityIdsFilter_DoesNotMatchOutdatedMunicipalityArea()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        // Outdated area with the requested Fid — must NOT match (IsCurrent = false)
        var outdatedArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "0301", Name = "Oslo (old)",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.Municipality,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = false
        };
        // Current area with a different Fid — what the projection would return
        var currentArea = new Area
        {
            Id = 2, DocumentId = "doc-2", Fid = "4631", Name = "Sogndal",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.Municipality,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(outdatedArea);
        location.Areas.Add(currentArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().AddRange(outdatedArea, currentArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        // Filter by the outdated Fid — should return nothing because the area is not current
        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { MunicipalityIds = ["0301"] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithMunicipalityIdsFilter_DoesNotReturnObservationMatchingOnlyCountyFid()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        // County area whose Fid happens to be in the filter — must NOT match
        var countyArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "03", Name = "Oslo County",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.County,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(countyArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().Add(countyArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        // Filter by the county's Fid — should return nothing because it's not a municipality area
        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { MunicipalityIds = ["03"] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithCountyIdsFilter_ReturnsObservationInMatchingCounty()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var countyArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "03", Name = "Oslo",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.County,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(countyArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().Add(countyArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CountyIds = ["03"] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCountyIdsFilter_DoesNotMatchOutdatedCountyArea()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var outdatedArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "03", Name = "Oslo (old)",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.County,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = false
        };
        var currentArea = new Area
        {
            Id = 2, DocumentId = "doc-2", Fid = "3024", Name = "Oslo (new)",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.County,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(outdatedArea);
        location.Areas.Add(currentArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().AddRange(outdatedArea, currentArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        // Filter by the outdated Fid — should return nothing because the area is not current
        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CountyIds = ["03"] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithCountyIdsFilter_DoesNotReturnObservationMatchingOnlyMunicipalityFid()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        // Municipality area whose Fid happens to be in the filter — must NOT match
        var municipalityArea = new Area
        {
            Id = 1, DocumentId = "doc-1", Fid = "0301", Name = "Oslo Municipality",
            AreaTypeId = (int)Core.Domain.Enums.AreaType.Municipality,
            ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow,
            TimeStamp = DateTime.UtcNow, IsCurrent = true
        };
        var location = CreateLocation(1, "Sentrum");
        location.Areas.Add(municipalityArea);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Area>().Add(municipalityArea);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 1));
        await context.SaveChangesAsync();

        // Filter by the municipality's Fid as a county filter — should return nothing because it's not a county area
        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CountyIds = ["0301"] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithOrganizationIdsFilter_ReturnsObservationLinkedToInstitutionOrg()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var org = new Organization
        {
            Id = 1, Name = "NHM",
            OrganizationTypeId = (int)Core.Domain.Enums.OrganizationType.Institution,
            DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow
        };
        var relation = new OrganizationRelation
        {
            Id = 1, ObservationId = 1, OrganizationId = 1, RelationTypeId = 1
        };

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Organization>().Add(org);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0));
        context.Set<OrganizationRelation>().Add(relation);
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { OrganizationIds = [1] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithOrganizationIdsFilter_ReturnsObservationRegardlessOfOrgType()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var org = new Organization
        {
            Id = 1, Name = "SomeCollection",
            OrganizationTypeId = (int)Core.Domain.Enums.OrganizationType.Collection,
            DateCreated = DateTime.UtcNow, DateModified = DateTime.UtcNow
        };
        var relation = new OrganizationRelation
        {
            Id = 1, ObservationId = 1, OrganizationId = 1, RelationTypeId = 1
        };

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Organization>().Add(org);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0));
        context.Set<OrganizationRelation>().Add(relation);
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { OrganizationIds = [1] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCategoryIdsFilter_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, categoryId: 5));
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0, categoryId: 10));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CategoryIds = [5] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCategoryIdsFilter_ExcludesObservationsWithOtherCategories()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, categoryId: 10));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CategoryIds = [5] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithBasisOfRecordIdsFilter_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, basisOfRecordId: 5));
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0, basisOfRecordId: 10));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { BasisOfRecordIds = [5] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithBasisOfRecordIdsFilter_ExcludesNonMatchingObservations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, basisOfRecordId: 10));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { BasisOfRecordIds = [5] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithBehaviorIdsFilter_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        var behavior = new Behavior { Id = 3, Name = "Hekking", Variants = "Hekking" };
        context.Set<Behavior>().Add(behavior);
        var observation = CreateObservation(1, locationId: 0);
        observation.Behaviors.Add(behavior);
        context.Set<Observation>().Add(observation);
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { BehaviorIds = [3] });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithBehaviorIdsFilter_ExcludesNonMatchingObservations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        var behavior = new Behavior { Id = 3, Name = "Hekking", Variants = "Hekking" };
        context.Set<Behavior>().Add(behavior);
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { BehaviorIds = [3] });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationsAsync_WithCoordinatePrecisionFrom_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, coordinatePrecisionInMeters: 50));
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0, coordinatePrecisionInMeters: 10));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 25 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCoordinatePrecisionTo_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, coordinatePrecisionInMeters: 50));
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0, coordinatePrecisionInMeters: 200));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { To = 100 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCoordinatePrecisionRange_ReturnsMatchingObservations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        context.Set<Observation>().Add(CreateObservation(1, locationId: 0, coordinatePrecisionInMeters: 50));
        context.Set<Observation>().Add(CreateObservation(2, locationId: 0, coordinatePrecisionInMeters: 5));
        context.Set<Observation>().Add(CreateObservation(3, locationId: 0, coordinatePrecisionInMeters: 500));
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 10, To = 100 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithPeriodFrom_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        var obs1 = CreateObservation(1, locationId: 0);
        obs1.DateTimeCollected = new DateTime(2022, 6, 1);
        var obs2 = CreateObservation(2, locationId: 0);
        obs2.DateTimeCollected = new DateTime(2019, 3, 1);
        context.Set<Observation>().AddRange(obs1, obs2);
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { Period = new PeriodDto { From = 2020 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithPeriodTo_ReturnsMatchingObservation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        var obs1 = CreateObservation(1, locationId: 0);
        obs1.DateTimeCollected = new DateTime(2020, 6, 1);
        var obs2 = CreateObservation(2, locationId: 0);
        obs2.DateTimeCollected = new DateTime(2024, 3, 1);
        context.Set<Observation>().AddRange(obs1, obs2);
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { Period = new PeriodDto { To = 2023 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithPeriodRange_ReturnsMatchingObservations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        context.Set<Taxon>().Add(CreateTaxon(1));
        var obs1 = CreateObservation(1, locationId: 0);
        obs1.DateTimeCollected = new DateTime(2021, 6, 1);
        var obs2 = CreateObservation(2, locationId: 0);
        obs2.DateTimeCollected = new DateTime(2018, 3, 1);
        var obs3 = CreateObservation(3, locationId: 0);
        obs3.DateTimeCollected = new DateTime(2025, 1, 1);
        context.Set<Observation>().AddRange(obs1, obs2, obs3);
        await context.SaveChangesAsync();

        var results = await sut.GetObservationsAsync(
            new ObservationSearchFilterDto { Period = new PeriodDto { From = 2020, To = 2023 } });

        results.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static SearchRepository CreateRepository(ArtskartDbContext context) =>
        new(context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()));

    private static void SeedLocations(ArtskartDbContext context, params Location[] locations) =>
        context.Set<Location>().AddRange(locations);

    private static void SeedObservations(ArtskartDbContext context, params Observation[] observations) =>
        context.Set<Observation>().AddRange(observations);

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
        string? institutionCode = "NHM",
        int? coordinatePrecisionInMeters = 25) =>
        new()
        {
            Id = id,
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            BasisOfRecordId = basisOfRecordId,
            TaxonId = 1,
            MatchedScientificNameId = 1,
            TaxonGroupId = taxonGroupId,
            CategoryId = categoryId,
            Latitude = 59.91,
            Longitude = 10.75,
            CoordinatePrecisionInMeters = coordinatePrecisionInMeters,
            East = 1000,
            North = 2000,
            LocationId = locationId,
            InstitutionCode = institutionCode,
            HashCode = id,
            ProcessEngineId = 1,
            HasAnnotations = false,
            HasErrors = false
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

    private static Taxon CreateTaxon(int id) =>
        new()
        {
            Id = id,
            TaxonRankId = 1,
            ExternalTaxonId = id,
            ParentTaxonId = 0,
            ValidScientificNameId = 1,
            TaxonGroupId = 1,
            ScientificNameIdHiarchy = id.ToString(),
            TaxonIdHiarchy = id.ToString()
        };

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}

using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NtsGeom = NetTopologySuite.Geometries;

namespace Artskart3.Tests.Unit;

public class SearchRepositoryTests
{
    [Fact]
    public async Task GetTaxonsAsync_WithNullName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

        var result = await sut.GetTaxonsAsync(null!);

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithWhitespaceName_ReturnsEmpty()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

        var result = await sut.GetTaxonsAsync("   ");

        result.Should().BeEmpty();
        contextMock.Verify(c => c.Set<Taxon>(), Times.Never);
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountZero_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

        var act = () => sut.GetTaxonsAsync("fugl", 0);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithMaxCountTooHigh_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

        var act = () => sut.GetTaxonsAsync("fugl", 1001);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("maxCount");
    }

    [Fact]
    public async Task GetTaxonsAsync_WithNegativeMaxCount_ThrowsArgumentException()
    {
        var contextMock = new Mock<IArtsKartDbContext>();
        var sut = new SearchRepository(contextMock.Object, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 1 } }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CategoryIds = new[] { 10 } }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { BasisOfRecordIds = new[] { 5 } }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 50 } }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { To = 50 } }));

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
            CoordinatePrecision = new CoordinatePrecisionDto { From = 25, To = 75 }
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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { MaxResults = 100001 }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 99 } }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = new[] { 1, 2 } }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = null }));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto()));

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

        var result = await ToListAsync(sut.GetLocationsAsync(new LocationSearchFilterDto()));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    // ---- GetLocationPolygonsAsync ----

    [Fact]
    public async Task GetLocationPolygonsAsync_WithNoObservations_ReturnsEmpty()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithNullGeometry_ExcludesLocation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocation(1, "Oslo")); // Geometry = null
        SeedObservations(context, CreateObservation(1, 1));
        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithRectangularPolygon5Points_ExcludesLocation()
    {
        // 5-point rectangle: NumPoints = 5, filtered out by the NumPoints > 5 query predicate
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocationWithGeometry(1, "Oslo", CreateRectangle5Pt()));
        SeedObservations(context, CreateObservation(1, 1));
        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithRectangularPolygon8Points_ExcludesLocation()
    {
        // 8-point rectangle (midpoints on each edge): passes NumPoints > 5 but caught by IsAxisAlignedRectangle
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocationWithGeometry(1, "Oslo", CreateRectangle8Pt()));
        SeedObservations(context, CreateObservation(1, 1));
        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithIrregularPolygon_IncludesLocation()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocationWithGeometry(1, "Oslo", CreateIrregularPolygon()));
        SeedObservations(context, CreateObservation(1, 1));
        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().ContainSingle();
        result[0].LocationId.Should().Be(1);
        result[0].Locality.Should().Be("Oslo");
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithMixedGeometries_ReturnsOnlyNonRectangular()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocationWithGeometry(1, "Oslo", CreateIrregularPolygon()),
            CreateLocationWithGeometry(2, "Bergen", CreateRectangle5Pt()),
            CreateLocationWithGeometry(3, "Trondheim", CreateRectangle8Pt()),
            CreateLocation(4, "Stavanger")); // null geometry

        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 2),
            CreateObservation(3, 3),
            CreateObservation(4, 4));

        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().ContainSingle();
        result[0].LocationId.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_ReturnsCorrectObservationCount()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context, CreateLocationWithGeometry(1, "Oslo", CreateIrregularPolygon()));
        SeedObservations(context,
            CreateObservation(1, 1),
            CreateObservation(2, 1),
            CreateObservation(3, 1));

        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync()).ToList();

        result.Should().ContainSingle();
        result[0].ObservationCount.Should().Be(3);
    }

    [Fact]
    public async Task GetLocationPolygonsAsync_WithTaxonGroupFilter_ExcludesNonMatchingLocations()
    {
        await using var context = CreateInMemoryContext();
        var sut = CreateRepository(context);

        SeedLocations(context,
            CreateLocationWithGeometry(1, "Oslo", CreateIrregularPolygon()),
            CreateLocationWithGeometry(2, "Bergen", CreateIrregularPolygon()));

        SeedObservations(context,
            CreateObservation(1, 1, taxonGroupId: 1),
            CreateObservation(2, 2, taxonGroupId: 2));

        await context.SaveChangesAsync();

        var result = (await sut.GetLocationPolygonsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [1] })).ToList();

        result.Should().ContainSingle();
        result[0].LocationId.Should().Be(1);
    }

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static SearchRepository CreateRepository(ArtskartDbContext context) =>
        new(context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService());

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
        int? coordinatePrecisionInMeters = 25,
        int taxonId = 1) =>
        new()
        {
            Id = id,
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

    private static Artskart3.Core.Domain.Entities.Location CreateLocationWithGeometry(int id, string locality, NtsGeom.Geometry geometry) =>
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
            Geometry = geometry
        };

    // Axis-aligned rectangle — 5 ring points, NumPoints = 5 (excluded by NumPoints > 5 query filter)
    private static NtsGeom.Geometry CreateRectangle5Pt() =>
        new NtsGeom.GeometryFactory().CreatePolygon(
        [
            new NtsGeom.Coordinate(0, 0),
            new NtsGeom.Coordinate(1000, 0),
            new NtsGeom.Coordinate(1000, 1000),
            new NtsGeom.Coordinate(0, 1000),
            new NtsGeom.Coordinate(0, 0),
        ]);

    // Axis-aligned rectangle with midpoints on each side — 9 ring points, NumPoints = 9
    // Passes the NumPoints > 5 filter; excluded by IsAxisAlignedRectangle (area == envelope area)
    private static NtsGeom.Geometry CreateRectangle8Pt() =>
        new NtsGeom.GeometryFactory().CreatePolygon(
        [
            new NtsGeom.Coordinate(0, 0),
            new NtsGeom.Coordinate(500, 0),
            new NtsGeom.Coordinate(1000, 0),
            new NtsGeom.Coordinate(1000, 500),
            new NtsGeom.Coordinate(1000, 1000),
            new NtsGeom.Coordinate(500, 1000),
            new NtsGeom.Coordinate(0, 1000),
            new NtsGeom.Coordinate(0, 500),
            new NtsGeom.Coordinate(0, 0),
        ]);

    // Irregular hexagon — 7 ring points, area < envelope area; passes all filters
    private static NtsGeom.Geometry CreateIrregularPolygon() =>
        new NtsGeom.GeometryFactory().CreatePolygon(
        [
            new NtsGeom.Coordinate(0, 0),
            new NtsGeom.Coordinate(1000, 0),
            new NtsGeom.Coordinate(1500, 500),
            new NtsGeom.Coordinate(1000, 1000),
            new NtsGeom.Coordinate(0, 1000),
            new NtsGeom.Coordinate(-500, 500),
            new NtsGeom.Coordinate(0, 0),
        ]);

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

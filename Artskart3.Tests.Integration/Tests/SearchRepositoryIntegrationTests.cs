using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class SearchRepositoryIntegrationTests : IAsyncLifetime
{
    private const int TestAreaTypeMunicipalityId = 910001;
    private const int TestAreaTypeCountyId = 910002;
    private const int TestBasisOfRecordOneId = 920001;
    private const int TestBasisOfRecordTwoId = 920002;
    private const int TestCategoryTypeId = 930001;
    private const int TestCategoryOneId = 930002;
    private const int TestCategoryTwoId = 930003;
    private const int TestTaxonGroupId = 940001;
    private const int TestTaxonRankId = 940002;
    private const int TestTaxonExactId = 950001;
    private const int TestTaxonStartsId = 950002;
    private const int TestTaxonContainsId = 950003;
    private const int TestTaxonDeletedId = 950004;
    private const int TestTaxonMissingObservationId = 950005;
    private const int TestTaxonNameExactId = 960001;
    private const int TestTaxonNameStartsId = 960002;
    private const int TestTaxonNameContainsId = 960003;
    private const int TestTaxonNameDeletedId = 960004;
    private const int TestTaxonNameMissingObservationId = 960005;
    private const int TestObservationTaxonGroupOneId = 970001;
    private const int TestObservationTaxonGroupTwoId = 970002;
    private const string CollectionOne = "REPO-COLL-A";
    private const string CollectionTwo = "REPO-COLL-B";

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private SearchRepository _repository = null!;
    private Location _locationOne = null!;
    private Location _locationTwo = null!;
    private Location _locationThree = null!;

    public SearchRepositoryIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        _repository = new SearchRepository(_context, NullLogger<SearchRepository>.Instance);

        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_ReturnsAreasOfSpecifiedType()
    {
        var result = (await _repository.GetAreasByTypeIdsAsync([TestAreaTypeMunicipalityId])).ToList();

        result.Should().OnlyContain(area => area.AreaTypeId == TestAreaTypeMunicipalityId);
        result.Select(area => area.Name).Should().Contain("Repo kommune A");
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_DoesNotReturnAreasOfOtherType()
    {
        var result = (await _repository.GetAreasByTypeIdsAsync([TestAreaTypeMunicipalityId])).ToList();

        result.Select(area => area.Name).Should().NotContain("Repo fylke A");
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_GroupsByName_SumsObservationCounts()
    {
        var result = (await _repository.GetAreasByTypeIdsAsync([TestAreaTypeMunicipalityId])).ToList();

        var groupedArea = result.Single(area => area.Name == "Repo gruppert kommune");
        groupedArea.ObservationCount.Should().Be(25);
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_WithEmptyTypeIds_ReturnsEmpty()
    {
        var result = await _repository.GetAreasByTypeIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAreasByTypeIdsAsync_SameNameDifferentAreaType_MergesIntoOneEntry()
    {
        var result = (await _repository.GetAreasByTypeIdsAsync([TestAreaTypeMunicipalityId, TestAreaTypeCountyId])).ToList();

        var mergedArea = result.Where(area => area.Name == "Repo felles område").ToList();
        mergedArea.Should().ContainSingle();
        mergedArea[0].ObservationCount.Should().Be(70);
    }

    [Fact]
    public async Task GetLocationsAsync_GroupsObservationsByLocation()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = $"{CollectionOne},{CollectionTwo}"
        }));

        result.Should().HaveCount(3);
        result.Should().Contain(model => model.Id == _locationOne.Id && model.ObservationCount == 3);
        result.Should().Contain(model => model.Id == _locationTwo.Id && model.ObservationCount == 2);
        result.Should().Contain(model => model.Id == _locationThree.Id && model.ObservationCount == 1);
    }

    [Fact]
    public async Task GetLocationsAsync_OrdersByObservationCountDescending()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = $"{CollectionOne},{CollectionTwo}"
        }));

        result.Select(location => location.ObservationCount).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByTaxonGroupId()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = TestObservationTaxonGroupOneId.ToString(),
            CollectionIds = $"{CollectionOne},{CollectionTwo}"
        }));

        result.Select(location => location.Id).Should().BeEquivalentTo([_locationOne.Id, _locationThree.Id]);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCategory()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            Categories = TestCategoryOneId.ToString()
        }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(_locationOne.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByBasisOfRecord()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            BasisOfRecords = TestBasisOfRecordOneId.ToString()
        }));

        result.Select(location => location.Id).Should().BeEquivalentTo([_locationOne.Id, _locationThree.Id]);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCollectionId()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = CollectionTwo
        }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(_locationTwo.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCoordinatePrecisionFrom()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = $"{CollectionOne},{CollectionTwo}",
            CoordinatePrecisionFrom = 100
        }));

        result.Select(location => location.Id).Should().BeEquivalentTo([_locationTwo.Id, _locationThree.Id]);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCoordinatePrecisionTo()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = $"{CollectionOne},{CollectionTwo}",
            CoordinatePrecisionTo = 50
        }));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(_locationOne.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_LimitsToMaxResults()
    {
        var result = await ToListAsync(_repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CollectionIds = $"{CollectionOne},{CollectionTwo}",
            MaxResults = 2
        }));

        result.Should().HaveCount(2);
        result.Select(location => location.Id).Should().ContainInOrder(_locationOne.Id, _locationTwo.Id);
    }

    [Fact]
    public async Task GetTaxonsAsync_ExactMatch_ReturnsTaxon()
    {
        var result = (await _repository.GetTaxonsAsync("repo-eksakt-art", 20)).ToList();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(TestTaxonExactId);
    }

    [Fact]
    public async Task GetTaxonsAsync_StartsWithMatch_ReturnsTaxon()
    {
        var result = (await _repository.GetTaxonsAsync("repo-start", 20)).ToList();

        result.Select(taxon => taxon.Id).Should().Contain(TestTaxonStartsId);
    }

    [Fact]
    public async Task GetTaxonsAsync_ContainsMatch_ReturnsTaxon()
    {
        var result = (await _repository.GetTaxonsAsync("inni", 20)).ToList();

        result.Select(taxon => taxon.Id).Should().Contain(TestTaxonContainsId);
    }

    [Fact]
    public async Task GetTaxonsAsync_ExcludesDeletedTaxons()
    {
        var result = await _repository.GetTaxonsAsync("repo-slettet-art", 20);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxonsAsync_ExcludesTaxaWithNoObservationsAndNotExistsInCountry()
    {
        var result = await _repository.GetTaxonsAsync("repo-uten-observasjon", 20);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxonsAsync_RespectsMaxCount()
    {
        var result = (await _repository.GetTaxonsAsync("repo", 2)).ToList();

        result.Should().HaveCount(2);
    }

    private async Task SeedTestDataAsync()
    {
        if (await _context.Set<TaxonGroup>().AnyAsync(group => group.Id == TestTaxonGroupId))
        {
            _locationOne = await _context.Set<Location>().SingleAsync(location => location.Locality == "Repo lokalitet 1");
            _locationTwo = await _context.Set<Location>().SingleAsync(location => location.Locality == "Repo lokalitet 2");
            _locationThree = await _context.Set<Location>().SingleAsync(location => location.Locality == "Repo lokalitet 3");
            return;
        }

        _context.Set<AreaType>().AddRange(
            new AreaType { Id = TestAreaTypeMunicipalityId, Name = "Kommune", CategoryName = "kommune", IsRequired = false },
            new AreaType { Id = TestAreaTypeCountyId, Name = "Fylke", CategoryName = "fylke", IsRequired = false });

        _context.Set<BasisOfRecord>().AddRange(
            new BasisOfRecord { Id = TestBasisOfRecordOneId, Name = "HumanObservation", Variants = "HumanObservation" },
            new BasisOfRecord { Id = TestBasisOfRecordTwoId, Name = "MachineObservation", Variants = "MachineObservation" });

        _context.Set<CategoryType>().Add(new CategoryType { Id = TestCategoryTypeId, Name = "Rødliste" });
        _context.Set<Category>().AddRange(
            new Category { Id = TestCategoryOneId, Code = "NT", Name = "Nær truet", CategoryTypeId = TestCategoryTypeId },
            new Category { Id = TestCategoryTwoId, Code = "CR", Name = "Kritisk truet", CategoryTypeId = TestCategoryTypeId });

        _context.Set<TaxonGroup>().Add(new TaxonGroup { Id = TestTaxonGroupId, Name = "Fugl" });
        _context.Set<TaxonRank>().Add(new TaxonRank { Id = TestTaxonRankId, Name = "Art" });

        _context.Set<Taxon>().AddRange(
            CreateTaxon(TestTaxonExactId, TestTaxonNameExactId, "repo-eksakt-art", false, 10, true),
            CreateTaxon(TestTaxonStartsId, TestTaxonNameStartsId, "repo-start-art", false, 10, true),
            CreateTaxon(TestTaxonContainsId, TestTaxonNameContainsId, "art-med-inni-navn", false, 10, true),
            CreateTaxon(TestTaxonDeletedId, TestTaxonNameDeletedId, "repo-slettet-art", true, 10, true),
            CreateTaxon(TestTaxonMissingObservationId, TestTaxonNameMissingObservationId, "repo-uten-observasjon", false, 0, false));

        _context.Set<TaxonName>().AddRange(
            new TaxonName { Id = TestTaxonNameExactId, Accepted = true, ScientificName = "repo-eksakt-art", TaxonId = TestTaxonExactId },
            new TaxonName { Id = TestTaxonNameStartsId, Accepted = true, ScientificName = "repo-start-art", TaxonId = TestTaxonStartsId },
            new TaxonName { Id = TestTaxonNameContainsId, Accepted = true, ScientificName = "art-med-inni-navn", TaxonId = TestTaxonContainsId },
            new TaxonName { Id = TestTaxonNameDeletedId, Accepted = true, ScientificName = "repo-slettet-art", TaxonId = TestTaxonDeletedId },
            new TaxonName { Id = TestTaxonNameMissingObservationId, Accepted = true, ScientificName = "repo-uten-observasjon", TaxonId = TestTaxonMissingObservationId });

        _context.Set<TaxonPopularName>().AddRange(
            new TaxonPopularName { SourceId = 1, Language = "nb", Name = "repo-eksakt-fugl", Preferred = true, TaxonId = TestTaxonExactId },
            new TaxonPopularName { SourceId = 1, Language = "nb", Name = "repo-start-fugl", Preferred = true, TaxonId = TestTaxonStartsId },
            new TaxonPopularName { SourceId = 1, Language = "nb", Name = "fugl-med-inni-navn", Preferred = true, TaxonId = TestTaxonContainsId },
            new TaxonPopularName { SourceId = 1, Language = "nb", Name = "repo-slettet-fugl", Preferred = true, TaxonId = TestTaxonDeletedId },
            new TaxonPopularName { SourceId = 1, Language = "nb", Name = "repo-uten-observasjon-fugl", Preferred = true, TaxonId = TestTaxonMissingObservationId });

        await _context.SaveChangesAsync();

        _locationOne = new Location
        {
            LookupId = "repo-loc-1",
            Latitude = 59.91,
            Longitude = 10.75,
            CoordinatePrecision = 25,
            East = 1000,
            North = 2000,
            Locality = "Repo lokalitet 1",
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = null
        };
        _locationTwo = new Location
        {
            LookupId = "repo-loc-2",
            Latitude = 60.39,
            Longitude = 5.32,
            CoordinatePrecision = 100,
            East = 1100,
            North = 2100,
            Locality = "Repo lokalitet 2",
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = null
        };
        _locationThree = new Location
        {
            LookupId = "repo-loc-3",
            Latitude = 63.43,
            Longitude = 10.39,
            CoordinatePrecision = 500,
            East = 1200,
            North = 2200,
            Locality = "Repo lokalitet 3",
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = null
        };

        _context.Set<Location>().AddRange(_locationOne, _locationTwo, _locationThree);
        await _context.SaveChangesAsync();

        _context.Set<Observation>().AddRange(
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 1),
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 2),
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 3),
            CreateObservation(_locationTwo.Id, TestObservationTaxonGroupTwoId, TestCategoryTwoId, TestBasisOfRecordTwoId, CollectionTwo, 100, 4),
            CreateObservation(_locationTwo.Id, TestObservationTaxonGroupTwoId, TestCategoryTwoId, TestBasisOfRecordTwoId, CollectionTwo, 100, 5),
            CreateObservation(_locationThree.Id, TestObservationTaxonGroupOneId, TestCategoryTwoId, TestBasisOfRecordOneId, CollectionOne, 500, 6));

        _context.Set<Area>().AddRange(
            CreateArea("Repo kommune A", TestAreaTypeMunicipalityId, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 15),
            CreateArea("Repo fylke A", TestAreaTypeCountyId, 20),
            CreateArea("Repo felles område", TestAreaTypeMunicipalityId, 30),
            CreateArea("Repo felles område", TestAreaTypeCountyId, 40));

        await _context.SaveChangesAsync();
    }

    private static Taxon CreateTaxon(int id, int validScientificNameId, string validScientificName, bool isDeleted, int cumulativeObservationCount, bool existsInCountry)
        => new()
        {
            Id = id,
            TaxonRankId = TestTaxonRankId,
            ExternalTaxonId = id,
            ParentTaxonId = 0,
            ValidScientificNameId = validScientificNameId,
            ValidScientificName = validScientificName,
            PreferredPopularName = validScientificName,
            TaxonGroupId = TestTaxonGroupId,
            ExistsInCountry = existsInCountry,
            ScientificNameIdHiarchy = id.ToString(),
            TaxonIdHiarchy = id.ToString(),
            CumulativeObservationCount = cumulativeObservationCount,
            IsDeleted = isDeleted
        };

    private static Observation CreateObservation(int locationId, int taxonGroupId, int categoryId, int basisOfRecordId, string institutionCode, int coordinatePrecision, int hashCode)
        => new()
        {
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            InstitutionCode = institutionCode,
            BasisOfRecordId = basisOfRecordId,
            TaxonId = TestTaxonExactId,
            MatchedScientificNameId = TestTaxonNameExactId,
            TaxonGroupId = taxonGroupId,
            CategoryId = categoryId,
            Latitude = 59.91,
            Longitude = 10.75,
            CoordinatePrecisionInMeters = coordinatePrecision,
            East = 1000,
            North = 2000,
            LocationId = locationId,
            HashCode = hashCode,
            ProcessEngineId = 1,
            HasErrors = false,
            HasAnnotations = false
        };

    private static Area CreateArea(string name, int areaTypeId, int observationCount)
        => new()
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = Guid.NewGuid().ToString("N"),
            Name = name,
            AreaTypeId = areaTypeId,
            ParentFid = "repo-parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = observationCount,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
            WktPolygon = null
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
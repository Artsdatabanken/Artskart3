using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        _repository = new SearchRepository(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService());

        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetLocationsAsync_GroupsObservationsByLocation()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId }
        });

        result.Should().HaveCount(3);
        result.Should().Contain(model => model.Id == _locationOne.Id && model.ObservationCount == 3);
        result.Should().Contain(model => model.Id == _locationTwo.Id && model.ObservationCount == 2);
        result.Should().Contain(model => model.Id == _locationThree.Id && model.ObservationCount == 1);
    }

    [Fact]
    public async Task GetLocationsAsync_OrdersByObservationCountDescending()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId }
        });

        result.Select(x => x.ObservationCount).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByTaxonGroupId()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId }
        });

        // Should return only locations that have observations with TestObservationTaxonGroupOneId
        result.Should().NotBeEmpty();
        result.Count().Should().BeGreaterThanOrEqualTo(2); // At least location 1 and 3
        result.Select(x => x.Id).Should().Contain(_locationOne.Id);
        result.Select(x => x.Id).Should().Contain(_locationThree.Id);
        // Should NOT contain location 2 which only has TaxonGroupTwoId
        result.Select(x => x.Id).Should().NotContain(_locationTwo.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByTaxonGroupIdOnly()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId }
        });

        result.Should().NotBeEmpty();
        result.Select(x => x.Id).Should().Contain(_locationOne.Id);
        result.Select(x => x.Id).Should().Contain(_locationThree.Id);
        result.Select(x => x.Id).Should().NotContain(_locationTwo.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCategory()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            CategoryIds = new[] { TestCategoryOneId }
        });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(_locationOne.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByBasisOfRecord()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            BasisOfRecordIds = new[] { TestBasisOfRecordOneId }
        });

        result.Select(location => location.Id).Should().BeEquivalentTo([_locationOne.Id, _locationThree.Id]);
    }

    // CollectionId-filtrering er erstattet av OrganizationIds via ObservationEntityIndex.
    // OrganizationIds-filtrering krever seeding av ObservationEntityIndex-tabellen.

    [Fact]
    public async Task GetLocationsAsync_FiltersByCoordinatePrecisionFrom()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId },
            CoordinatePrecision = new CoordinatePrecisionDto { From = 100 }
        });

        result.Select(location => location.Id).Should().BeEquivalentTo([_locationTwo.Id, _locationThree.Id]);
    }

    [Fact]
    public async Task GetLocationsAsync_FiltersByCoordinatePrecisionTo()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId },
            CoordinatePrecision = new CoordinatePrecisionDto { To = 50 }
        });

        result.Should().ContainSingle();
        result[0].Id.Should().Be(_locationOne.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_LimitsToMaxResults()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId },
            MaxResults = 2
        });

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

    [Fact]
    public async Task GetAreaCountsAsync_MixedRankTaxonIds_ExecutesTranslatedQuery()
    {
        // Regresjon: blanding av orden-rang (direkte kolonnefilter) og et taxon som
        // faller tilbake til ObservationTaxonHierarchy ga en OR-lambda med null-sjekk
        // på en captured IQueryable, som EF ikke kunne oversette til SQL.
        const int orderTaxonId = 980001;
        const int phylumTaxonId = 980002;

        var hierarchy = new ConfigurableTaxonHierarchyService(new Dictionary<int, int>
        {
            [orderTaxonId] = 11,  // Orden
            [phylumTaxonId] = 3,  // Rekke (Phylum)
        });
        var repository = new SearchRepository(
            _context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), hierarchy);

        var act = () => repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            TaxonIds = [orderTaxonId, phylumTaxonId]
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAreaCountsAsync_HierarchyFallbackTaxon_ReturnsNonZeroCount()
    {
        // Bekrefter at en kollapset forelder-id (phylum, ingen orden-etterkommere) som går via
        // ObservationTaxonHierarchy-fallback faktisk gir treff — ikke et stille tomt resultat.
        const int phylumTaxonId = 980002;
        const int areaEntityId = 980010;
        var areaFid = $"980010";

        var hierarchy = new ConfigurableTaxonHierarchyService(new Dictionary<int, int>
        {
            [phylumTaxonId] = 3,  // Rekke (Phylum)
        });
        var repository = new SearchRepository(
            _context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), hierarchy);

        // Seed: område på zoomnivå 2 + indeks- og hierarkirad. AreaCounts-spørringen
        // berører kun indeks- og hierarkitabellen, så ingen Observation-rad trengs —
        // og vi unngår å forurense de delte lokasjonsantallene.
        const int syntheticObservationId = 980500;
        await RemoveSyntheticRowsAsync(syntheticObservationId, areaFid);

        _context.Set<Area>().Add(new Area
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = areaFid,
            Name = "Ancestry test kommune",
            AreaTypeId = TestAreaTypeMunicipalityId,
            ZoomLevel = 2,
            ParentFid = "repo-parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = 0,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
        });
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = syntheticObservationId,
            EntityTypeId = TestAreaTypeMunicipalityId,
            EntityId = areaEntityId,
            TaxonGroupId = TestObservationTaxonGroupOneId,
            BasisOfRecordId = TestBasisOfRecordOneId,
            RegistrationStatusId = 0,
        });
        _context.Set<ObservationTaxonHierarchy>().Add(new ObservationTaxonHierarchy
        {
            ObservationId = syntheticObservationId,
            PhylumTaxonId = phylumTaxonId,
        });
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            TaxonIds = [phylumTaxonId]
        });

        var area = result.SingleOrDefault(a => a.Fid == areaFid);
        area.Should().NotBeNull("området har en matchende observasjon via hierarki-fallback");
        area!.ObservationCount.Should().Be(1);
    }

    /// <summary>
    /// Mellomnivåer har ingen egen kolonne i ObservationEntityIndex (kun eksakt rang
    /// 11/15/19/22 — se BackfillTaxonHierarchyColumns.sql). De skal derfor matches via
    /// ObservationTaxonHierarchy, som har en kolonne per rangnivå.
    /// </summary>
    [Theory]
    [InlineData(12, nameof(ObservationTaxonHierarchy.SuborderTaxonId))]
    [InlineData(16, nameof(ObservationTaxonHierarchy.SubfamilyTaxonId))]
    [InlineData(20, nameof(ObservationTaxonHierarchy.SubgenusTaxonId))]
    [InlineData(23, nameof(ObservationTaxonHierarchy.SubspeciesTaxonId))]
    [InlineData(26, nameof(ObservationTaxonHierarchy.NotSetTaxonId))]
    public async Task GetAreaCountsAsync_OffRankTaxon_UsesHierarchyFallback(int rankId, string hierarchyColumn)
    {
        var taxonId = 980100 + rankId;
        var areaEntityId = 980200 + rankId;
        var areaFid = areaEntityId.ToString();

        var hierarchy = new ConfigurableTaxonHierarchyService(new Dictionary<int, int>
        {
            [taxonId] = rankId,
        });
        var repository = new SearchRepository(
            _context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), hierarchy);

        _context.Set<Area>().Add(new Area
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = areaFid,
            Name = $"OffRank {rankId} test kommune",
            AreaTypeId = TestAreaTypeMunicipalityId,
            ZoomLevel = 2,
            ParentFid = "repo-parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = 0,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
        });
        // Kun indeks- og hierarkirad — AreaCounts berører ikke Observation-tabellen,
        // og vi unngår å forurense de delte lokasjonsantallene.
        var syntheticObservationId = 980500 + rankId;
        await RemoveSyntheticRowsAsync(syntheticObservationId, areaFid);

        // Indeksraden har ingen denormalisert rangverdi; hierarkiraden bærer mellomnivået
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = syntheticObservationId,
            EntityTypeId = TestAreaTypeMunicipalityId,
            EntityId = areaEntityId,
            TaxonGroupId = TestObservationTaxonGroupOneId,
            BasisOfRecordId = TestBasisOfRecordOneId,
            RegistrationStatusId = 0,
        });
        var hierarchyRow = new ObservationTaxonHierarchy { ObservationId = syntheticObservationId };
        typeof(ObservationTaxonHierarchy).GetProperty(hierarchyColumn)!.SetValue(hierarchyRow, taxonId);
        _context.Set<ObservationTaxonHierarchy>().Add(hierarchyRow);
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            TaxonIds = [taxonId]
        });

        var area = result.SingleOrDefault(a => a.Fid == areaFid);
        area.Should().NotBeNull($"rang {rankId} matcher via hierarkitabellen");
        area!.ObservationCount.Should().Be(1);
    }

    /// <summary>
    /// Gjør seedingen av syntetiske rader idempotent på tvers av testkjøringer
    /// (databasen deles og tilbakestilles ikke mellom kjøringer).
    /// </summary>
    private async Task RemoveSyntheticRowsAsync(int observationId, string areaFid)
    {
        _context.Set<ObservationTaxonHierarchy>()
            .RemoveRange(_context.Set<ObservationTaxonHierarchy>().Where(h => h.ObservationId == observationId));
        _context.Set<ObservationEntityIndex>()
            .RemoveRange(_context.Set<ObservationEntityIndex>().Where(i => i.ObservationId == observationId));
        _context.Set<Area>()
            .RemoveRange(_context.Set<Area>().Where(a => a.Fid == areaFid));
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// ITaxonHierarchyService med konfigurerbare rangnivåer. Taxa uten oppføring
    /// håndteres som ukjente. Ingen taxa har etterkommere på annet rangnivå,
    /// slik at høyere-rangs taxa faller tilbake til ObservationTaxonHierarchy.
    /// </summary>
    private sealed class ConfigurableTaxonHierarchyService(Dictionary<int, int> ranks)
        : Artskart3.Core.Application.Services.Interfaces.ITaxonHierarchyService
    {
        public int? GetTaxonRankId(int taxonId) => ranks.TryGetValue(taxonId, out var rank) ? rank : null;

        public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId) => [];

        public List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds) =>
            taxonIds.Select(id => new TaxonAncestryDto { Id = id, ParentIds = [] }).ToList();

        public List<int> GetDescendantSpeciesIds(int taxonId) => [];

        public List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId) => [];
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
            CreateArea("Repo kommune A", TestAreaTypeMunicipalityId, 10, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 10, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 15, 10),
            CreateArea("Repo fylke A", TestAreaTypeCountyId, 20, 7),
            CreateArea("Repo felles område", TestAreaTypeMunicipalityId, 30, 10),
            CreateArea("Repo felles område", TestAreaTypeCountyId, 40, 7));

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

    private static Area CreateArea(string name, int areaTypeId, int observationCount, int zoomLevel)
        => new()
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = Guid.NewGuid().ToString("N"),
            Name = name,
            AreaTypeId = areaTypeId,
            ZoomLevel = zoomLevel,
            ParentFid = "repo-parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = observationCount,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
            WktPolygon = null
        };

}

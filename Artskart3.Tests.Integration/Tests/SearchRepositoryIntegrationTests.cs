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
using ObservationIndexEntityType = Artskart3.Core.Domain.Enums.ObservationIndexEntityType;
using RegistrationStatus = Artskart3.Core.Domain.Enums.RegistrationStatus;
using TagEnum = Artskart3.Core.Domain.Enums.Tag;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class SearchRepositoryIntegrationTests : IAsyncLifetime
{
    private const int TestAreaTypeMunicipalityId = 910001;

    // Datasett-id som garantert ikke er knyttet til noen observasjon.
    private const int UnusedDatasetOrgId = 987654321;
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

    /// <summary>
    /// Områdeid for indeksradene til grunnoppsettets observasjoner. Ligger bevisst
    /// utenfor 981xxx-rekkevidden CompleteFilter-testene bruker, og ingen Area i
    /// grunnoppsettet har numerisk Fid, så radene påvirker ikke områdetellingene.
    /// </summary>
    private const int TestLocationsAreaEntityId = 970010;
    // CompleteFilter: samling er en Organization-ID, ikke lenger en kode-streng.
    private const int CollectionOne = 1;
    private const int CollectionTwo = 2;
    private const string CadastralUnitId = "1101-1/2";

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
        _repository = new SearchRepository(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()), new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

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
    public async Task GetLocationsAsync_SetsRegistrationStatusFromAllObservationsAtLocation()
    {
        var filter = new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId }
        };
        var observationIds = await _context.Set<Observation>()
            .Where(o => o.LocationId == _locationTwo.Id)
            .OrderBy(o => o.Id).Select(o => o.Id).ToListAsync();
        observationIds.Should().HaveCount(2);

        async Task<RegistrationStatus> StatusOf(Location location) =>
            (await _repository.GetLocationsAsync(filter)).Single(l => l.Id == location.Id).RegistrationStatus;

        try
        {
            await AddTagRowAsync(observationIds[0], TagEnum.Absent);
            (await StatusOf(_locationTwo)).Should().Be(RegistrationStatus.Found);

            await AddTagRowAsync(observationIds[1], TagEnum.Absent);
            await AddTagRowAsync(observationIds[1], TagEnum.NotRecovered);
            (await StatusOf(_locationTwo)).Should().Be(RegistrationStatus.NotRecovered);

            await RemoveSyntheticTagRowAsync(observationIds[1], (int)TagEnum.NotRecovered);
            (await StatusOf(_locationTwo)).Should().Be(RegistrationStatus.Absent);
            (await StatusOf(_locationOne)).Should().Be(RegistrationStatus.Found);
        }
        finally
        {
            foreach (var observationId in observationIds)
            {
                await RemoveSyntheticTagRowAsync(observationId, (int)TagEnum.Absent);
                await RemoveSyntheticTagRowAsync(observationId, (int)TagEnum.NotRecovered);
            }
        }
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

    /// <summary>
    /// Uten underliggende taksa matches observasjonens eget TaxonId, ikke hierarkitabellen.
    /// Alle testobservasjonene er bestemt til TestTaxonExactId.
    /// </summary>
    [Fact]
    public async Task GetLocationsAsync_WithoutDescendantTaxa_MatchesObservationTaxonId()
    {
        var result = await _repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonIds = [TestTaxonExactId],
            IncludeDescendantTaxa = false,
        });

        result.Should().Contain(model => model.Id == _locationOne.Id && model.ObservationCount == 3);
        result.Should().Contain(model => model.Id == _locationTwo.Id && model.ObservationCount == 2);
        result.Should().Contain(model => model.Id == _locationThree.Id && model.ObservationCount == 1);
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

    /// <summary>
    /// Antallet teller fra samme kilde som lokasjonssøket, så de to må gi samme tall
    /// for samme filter. Går denne i stå, har de to kodestiene glidd fra hverandre.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetLocationCountAsync_StemmerMedGetLocationsAsync(bool filtrert)
    {
        var filter = filtrert
            ? new LocationSearchFilterDto { TaxonGroupIds = new[] { TestObservationTaxonGroupOneId } }
            : new LocationSearchFilterDto
            {
                TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId }
            };

        var lokasjoner = await _repository.GetLocationsAsync(filter);
        var antall = await _repository.GetLocationCountAsync(filter);

        antall.Count.Should().Be(lokasjoner.Count);
        antall.Truncated.Should().BeFalse();
    }

    /// <summary>
    /// MaxResults gjelder lokasjonssøket, ikke antallet. Antallet har sitt eget tak
    /// (MaxLocationResults) og skal fortsette å telle alle treff selv om kalleren
    /// ber om få lokasjoner.
    /// </summary>
    [Fact]
    public async Task GetLocationCountAsync_IgnorererMaxResults()
    {
        var filter = new LocationSearchFilterDto
        {
            TaxonGroupIds = new[] { TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId },
            MaxResults = 2
        };

        var antall = await _repository.GetLocationCountAsync(filter);

        antall.Count.Should().Be(3);
        antall.Truncated.Should().BeFalse();
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
            new StubAreaHierarchyService(), hierarchy, new StubCadastralUnitService());

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
            new StubAreaHierarchyService(), hierarchy, new StubCadastralUnitService());

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
    /// 11/15/19/22 — se Scripts/BackfillAll.sql seksjon D). De skal derfor matches via
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
            new StubAreaHierarchyService(), hierarchy, new StubCadastralUnitService());

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
    // -----------------------------------------------------------------------
    // CompleteFilter: filtre som besvares direkte fra ObservationEntityIndex
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreaCountsAsync_BehaviorFilter_CountsOnlyMatchingBehavior()
    {
        const int observationId = 981000;
        const string areaFid = "981010";
        const int areaEntityId = 981010;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        SeedArea(areaFid, "Atferd test kommune");
        SeedIndexRow(observationId, areaEntityId, behaviorId: 2);
        await _context.SaveChangesAsync();

        var treff = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { BehaviorIds = [2] });
        var bom = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { BehaviorIds = [3] });

        treff.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        bom.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0);
    }

    /// <summary>
    /// BehaviorId er tinyint. C# caster unchecked, så (byte)257 blir 1: en
    /// forespørsel med behaviorIds=[257] ville talt atferd 1 på kartet, mens
    /// listevisningen - som sammenligner rå int - korrekt ikke fant noe. Ikke
    /// ufiltrert, men stille forskjovet, som er verre å oppdage.
    /// </summary>
    [Theory]
    [InlineData(257)]
    [InlineData(-1)]
    [InlineData(256)]
    public async Task GetAreaCountsAsync_BehaviorIdOutsideByteRange_ReturnsNoCounts(int behaviorId)
    {
        const string areaFid = "981110";
        const int areaEntityId = 981110;

        var repository = CreateCompleteFilterRepository();

        // En rad per verdi de tre inndataene ville kollapset til med unchecked cast:
        // (byte)257 = 1, (byte)-1 = 255, (byte)256 = 0. Uten en rad å treffe ville
        // testen bestått også med vakten fjernet - den ville målt ingenting.
        foreach (var (observationId, seededBehaviorId) in new[] { (981100, 1), (981101, 255), (981102, 0) })
        {
            await RemoveSyntheticRowsAsync(observationId, areaFid);
            SeedIndexRow(observationId, areaEntityId, behaviorId: (byte)seededBehaviorId);
        }

        SeedArea(areaFid, "Atferd overflow kommune");
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            BehaviorIds = [behaviorId]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0,
            "en atferds-id utenfor tinyint-omraadet har ingen treff og skal aldri kollapse til en gyldig verdi");
    }

    [Fact]
    public async Task GetAreaCountsAsync_CollectionFilter_CountsOnlyMatchingCollection()
    {
        const int observationId = 981200;
        const string areaFid = "981210";
        const int areaEntityId = 981210;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        SeedArea(areaFid, "Samling test kommune");
        SeedIndexRow(observationId, areaEntityId, datasetOrgId: 26435);
        await _context.SaveChangesAsync();

        var treff = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { DatasetOrgIds = [26435] });
        var bom = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { DatasetOrgIds = [26436] });

        treff.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        bom.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0);
    }

    /// <summary>
    /// Institusjonsfilteret gikk tidligere via self-join mot EntityTypeId=101-radene.
    /// Kolonnen erstattet både joinen og radene.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_InstitutionFilter_CountsOnlyMatchingInstitution()
    {
        const int observationId = 981300;
        const string areaFid = "981310";
        const int areaEntityId = 981310;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        SeedArea(areaFid, "Institusjon test kommune");
        SeedIndexRow(observationId, areaEntityId, institutionOrgId: 2046);
        await _context.SaveChangesAsync();

        var treff = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { OrganizationIds = [2046] });
        var bom = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { OrganizationIds = [2047] });

        treff.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        bom.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0);
    }

    /// <summary>
    /// Flere valgte institusjoner er ETT filter med OR internt: velger man to, skal
    /// begge telle med.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_MultipleInstitutions_CountsAllOfThem()
    {
        const int firstObservationId = 981400;
        const int secondObservationId = 981401;
        const string areaFid = "981410";
        const int areaEntityId = 981410;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(firstObservationId, areaFid);
        await RemoveSyntheticRowsAsync(secondObservationId, areaFid);
        SeedArea(areaFid, "Institusjon OR kommune");
        SeedIndexRow(firstObservationId, areaEntityId, institutionOrgId: 2046);
        SeedIndexRow(secondObservationId, areaEntityId, institutionOrgId: 2047);
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            OrganizationIds = [2046, 2047]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(2);
    }

    /// <summary>
    /// Katalognummer er allerede løst opp til ObservationId-er av typeaheaden, så
    /// filteret er et seek på klyngeindeksen.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_ObservationIdsFilter_CountsOnlyThoseObservations()
    {
        const int matchingObservationId = 981500;
        const int otherObservationId = 981501;
        const string areaFid = "981510";
        const int areaEntityId = 981510;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(matchingObservationId, areaFid);
        await RemoveSyntheticRowsAsync(otherObservationId, areaFid);
        SeedArea(areaFid, "Katalognummer test kommune");
        SeedIndexRow(matchingObservationId, areaEntityId);
        SeedIndexRow(otherObservationId, areaEntityId);
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            ObservationIds = [matchingObservationId]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
    }

    /// <summary>
    /// Uten underliggende taksa slås observasjonens eget takson opp i Observation,
    /// siden indekstabellen ikke har en kolonne for det.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_WithoutDescendantTaxa_CountsOnlyObservationsOfThatTaxon()
    {
        const int indexOnlyObservationId = 981800;
        const string areaFid = "981810";
        const int areaEntityId = 981810;

        var observationId = await _context.Set<Observation>()
            .Where(o => o.TaxonId == TestTaxonExactId)
            .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();

        var repository = CreateCompleteFilterRepository();

        // observationId er en EKTE seedet observasjon — den er valgt fordi filteret
        // krever en Observation-rad med dette taksonet. Da kan opprydningen bare
        // fjerne radene denne testen selv legger til: RemoveSyntheticRowsAsync ville
        // slettet ALLE indeks- og hierarkiradene dens, og den har rader på
        // _locationOne som lokasjonstestene teller.
        await RemoveSyntheticAreaRowsAsync(observationId, areaEntityId, areaFid);
        await RemoveSyntheticRowsAsync(indexOnlyObservationId, areaFid);
        SeedArea(areaFid, "Uten underliggende taksa kommune");
        SeedIndexRow(observationId, areaEntityId);
        SeedIndexRow(indexOnlyObservationId, areaEntityId);
        await _context.SaveChangesAsync();

        var treff = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            TaxonIds = [TestTaxonExactId],
            IncludeDescendantTaxa = false,
        });
        var bom = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            TaxonIds = [TestTaxonStartsId],
            IncludeDescendantTaxa = false,
        });

        treff.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        bom.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0);
    }

    [Fact]
    public async Task GetAreaCountsAsync_DatasetFilter_CountsObservationsInThatDataset()
    {
        const string areaFid = "981610";
        const int areaEntityId = 981610;

        // ObservationProject har fremmednokler til BADE Observation og Organization,
        // så raden må henge på noe som finnes. Vi låner en observasjon fra
        // testdataene i stedet for å lage en ny - en ny observasjon ville endret
        // lokasjonstellingene som de andre testene i samlingen bygger på.
        var observationId = await _context.Set<Observation>()
            .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        await RemoveSyntheticDatasetRowsAsync(observationId);
        var projectOrgId = (await CreateDatasetOrganizationsAsync(1))[0];
        SeedArea(areaFid, "Prosjekt test kommune");
        SeedIndexRow(observationId, areaEntityId);
        _context.Set<ObservationProject>().Add(new ObservationProject
        {
            ObservationId = observationId,
            ProjectOrgId = projectOrgId,
        });
        await _context.SaveChangesAsync();

        var treff = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { ProjectOrgIds = [projectOrgId] });
        var bom = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto { ProjectOrgIds = [UnusedDatasetOrgId] });

        treff.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        bom.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(0);
    }

    /// <summary>
    /// 745 066 observasjoner tilhorer mellom to og fem datasett. Derfor er koblingen
    /// en egen tabell, og derfor må filteret være et semi-join: et join ville talt
    /// observasjonen en gang per datasett og blåst opp områdetellingen.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_ObservationInSeveralDatasets_IsCountedOnce()
    {
        const string areaFid = "981710";
        const int areaEntityId = 981710;

        var observationId = await _context.Set<Observation>()
            .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        await RemoveSyntheticDatasetRowsAsync(observationId);
        var datasetOrgIds = await CreateDatasetOrganizationsAsync(3);
        var projectOrgId = datasetOrgIds[0];
        SeedArea(areaFid, "Flere datasett kommune");
        SeedIndexRow(observationId, areaEntityId);
        _context.Set<ObservationProject>().AddRange(
            datasetOrgIds.Select(id => new ObservationProject
            {
                ObservationId = observationId,
                ProjectOrgId = id,
            }));
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            ProjectOrgIds = [projectOrgId]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
    }

    /// <summary>
    /// Flere valgte datasett er ETT filter med OR internt, som institusjoner.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_MultipleDatasets_CountsAllOfThem()
    {
        const int firstObservationId = 981800;
        const int secondObservationId = 981801;
        const int otherObservationId = 981802;
        const string areaFid = "981810";
        const int areaEntityId = 981810;

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(firstObservationId, areaFid);
        await RemoveSyntheticRowsAsync(secondObservationId, areaFid);
        await RemoveSyntheticRowsAsync(otherObservationId, areaFid);
        SeedArea(areaFid, "Datasett OR kommune");
        SeedIndexRow(firstObservationId, areaEntityId, datasetOrgId: 26435);
        SeedIndexRow(secondObservationId, areaEntityId, datasetOrgId: 26436);
        SeedIndexRow(otherObservationId, areaEntityId, datasetOrgId: 26437);
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            DatasetOrgIds = [26435, 26436]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(2);
    }

    /// <summary>
    /// En observasjon i to av de valgte prosjektene skal fortsatt telles én gang.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_ObservationInSeveralSelectedProjects_IsCountedOnce()
    {
        const string areaFid = "981910";
        const int areaEntityId = 981910;

        var observationId = await _context.Set<Observation>()
            .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(observationId, areaFid);
        await RemoveSyntheticDatasetRowsAsync(observationId);
        var projectOrgIds = await CreateDatasetOrganizationsAsync(3);
        SeedArea(areaFid, "Flere valgte prosjekter kommune");
        SeedIndexRow(observationId, areaEntityId);
        _context.Set<ObservationProject>().AddRange(
            projectOrgIds.Select(id => new ObservationProject
            {
                ObservationId = observationId,
                ProjectOrgId = id,
            }));
        await _context.SaveChangesAsync();

        var result = await repository.GetAreaCountsAsync(2, new LocationSearchFilterDto
        {
            ProjectOrgIds = [projectOrgIds[0], projectOrgIds[1]]
        });

        result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
    }

    // Tagfiltrene leser denormaliserte kolonner på ObservationEntityIndex, ikke
    // koblingstabellen. Testene setter BEGGE: taggen beskriver kilden, kolonnen er
    // det spørringen faktisk filtrerer på. Går de fra hverandre i
    // produksjonskoden — en backfill som ikke dekker alt — er det det disse skal
    // avsløre. Samme mønster som bildetesten i ObservationQueryBuilderTests.

    [Fact]
    public Task GetAreaCountsAsync_ChangedByAnnotationFilter_SplitsOnAnnotatedChangesTag() =>
        AssertAreaCountsSplitOnTagAsync(982000, TagEnum.AnnotatedChanges,
            hasTag => new LocationSearchFilterDto { ChangedByAnnotation = hasTag });

    [Fact]
    public Task GetAreaCountsAsync_SensitiveFilter_SplitsOnSensitiveTag() =>
        AssertAreaCountsSplitOnTagAsync(983000, TagEnum.Sensitive,
            hasTag => new LocationSearchFilterDto { Sensitive = hasTag });

    [Fact]
    public Task GetAreaCountsAsync_UncertainIdentificationFilter_SplitsOnUnsureIdentificationTag() =>
        AssertAreaCountsSplitOnTagAsync(984000, TagEnum.UnsureIdentification,
            hasTag => new LocationSearchFilterDto { UncertainIdentification = hasTag });

    [Fact]
    public Task GetAreaCountsAsync_IntroducedFilter_SplitsOnUnspontaneusTag() =>
        AssertAreaCountsSplitOnTagAsync(985000, TagEnum.Unspontaneus,
            hasTag => new LocationSearchFilterDto { Introduced = hasTag });

    private async Task AssertAreaCountsSplitOnTagAsync(
        int untaggedObservationId, TagEnum tag, Func<bool, LocationSearchFilterDto> filterOnTag)
    {
        var areaEntityId = untaggedObservationId + 10;
        var areaFid = areaEntityId.ToString();
        var tagId = (int)tag;

        // ObservationTags har fremmednøkkel til Observation, så den taggede
        // observasjonen lånes fra testdataene, som i datasettestene over.
        var taggedObservationId = await _context.Set<Observation>()
            .OrderBy(o => o.Id).Select(o => o.Id).FirstAsync();

        var repository = CreateCompleteFilterRepository();
        await RemoveSyntheticRowsAsync(taggedObservationId, areaFid);
        await RemoveSyntheticRowsAsync(untaggedObservationId, areaFid);
        await RemoveSyntheticTagRowAsync(taggedObservationId, tagId);
        SeedArea(areaFid, $"{tag} test kommune");
        SeedIndexRow(taggedObservationId, areaEntityId,
            changedByAnnotation: tag == TagEnum.AnnotatedChanges,
            sensitive: tag == TagEnum.Sensitive,
            uncertainIdentification: tag == TagEnum.UnsureIdentification,
            introduced: tag == TagEnum.Unspontaneus);
        SeedIndexRow(untaggedObservationId, areaEntityId);
        await AddTagRowAsync(taggedObservationId, tag);

        var withTag = await repository.GetAreaCountsAsync(2, filterOnTag(true));
        var withoutTag = await repository.GetAreaCountsAsync(2, filterOnTag(false));

        withTag.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
        withoutTag.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(1);
    }

    [Fact]
    public async Task GetLocationsAsync_WithCadastralUnit_ReturnsOnlyLocationsInsideIt()
    {
        var repository = CreateRepositoryWithCadastralUnit(_locationTwo.Id);

        var result = await repository.GetLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId],
            CadastralUnitIds = [CadastralUnitId],
        });

        result.Should().ContainSingle().Which.Id.Should().Be(_locationTwo.Id);
    }

    [Fact]
    public async Task GetLocationsAsync_WithCadastralUnitAndMunicipality_ReturnsLocationsInEither()
    {
        const int municipalityEntityId = 981910;
        var indexRow = await SeedMunicipalityRowAsync(municipalityEntityId, _locationOne);

        try
        {
            var result = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetLocationsAsync(new LocationSearchFilterDto
            {
                TaxonGroupIds = [TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId],
                MunicipalityIds = [municipalityEntityId.ToString()],
                CadastralUnitIds = [CadastralUnitId],
            });

            result.Select(l => l.Id).Should().BeEquivalentTo([_locationOne.Id, _locationTwo.Id]);
        }
        finally
        {
            _context.Remove(indexRow);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Områdene og matrikkelenhetene slås sammen med UNION ALL, så en observasjon i
    /// begge kommer fra begge sider. Den skal likevel telles én gang.
    /// </summary>
    [Fact]
    public async Task GetLocationsAsync_WithObservationInBothCadastralUnitAndMunicipality_CountsItOnce()
    {
        const int municipalityEntityId = 981950;
        var indexRow = await SeedMunicipalityRowAsync(municipalityEntityId, _locationTwo);

        try
        {
            var filter = new LocationSearchFilterDto
            {
                TaxonGroupIds = [TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId],
                MunicipalityIds = [municipalityEntityId.ToString()],
                CadastralUnitIds = [CadastralUnitId],
            };
            var repository = CreateRepositoryWithCadastralUnit(_locationTwo.Id);
            var atLocationTwo = await repository.GetLocationsAsync(new LocationSearchFilterDto
            {
                TaxonGroupIds = filter.TaxonGroupIds,
                CadastralUnitIds = [CadastralUnitId],
            });

            var result = await repository.GetLocationsAsync(filter);
            var count = await repository.GetLocationCountAsync(filter);

            result.Should().ContainSingle().Which.ObservationCount.Should().Be(atLocationTwo.Single().ObservationCount);
            count.Count.Should().Be(1);
        }
        finally
        {
            _context.Remove(indexRow);
            await _context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetLocationCountAsync_WithCadastralUnit_CountsOnlyItsLocations()
    {
        var antall = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetLocationCountAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId],
            CadastralUnitIds = [CadastralUnitId],
        });

        antall.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetObservationsAsync_WithCadastralUnit_ReturnsOnlyObservationsAtItsLocations()
    {
        var expected = await _context.Set<Observation>()
            .Where(o => o.LocationId == _locationTwo.Id)
            .Select(o => o.Id)
            .ToListAsync();

        var result = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetObservationsAsync(new ObservationSearchFilterDto
        {
            CadastralUnitIds = [CadastralUnitId],
        });

        result.Select(o => o.Id).Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// Områdestien har ingen gren for matrikkelenheter, så med begge i filteret skal
    /// grenstien (to taksongrupper) ta over og OR-e dem.
    /// </summary>
    [Fact]
    public async Task GetObservationsAsync_WithCadastralUnitAndMunicipality_ReturnsObservationsInEither()
    {
        const int municipalityEntityId = 981940;
        int[] taxonGroupIds = [TestObservationTaxonGroupOneId, TestObservationTaxonGroupTwoId];
        var indexRow = await SeedMunicipalityRowAsync(municipalityEntityId, _locationOne);

        try
        {
            var atLocationTwo = await _context.Set<Observation>()
                .Where(o => o.LocationId == _locationTwo.Id && taxonGroupIds.Contains(o.TaxonGroupId))
                .Select(o => o.Id)
                .ToListAsync();

            var result = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetObservationsAsync(new ObservationSearchFilterDto
            {
                TaxonGroupIds = taxonGroupIds,
                MunicipalityIds = [municipalityEntityId.ToString()],
                CadastralUnitIds = [CadastralUnitId],
            });

            result.Select(o => o.Id).Should().BeEquivalentTo([indexRow.ObservationId, .. atLocationTwo]);
        }
        finally
        {
            _context.Remove(indexRow);
            await _context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAreaCountsAsync_WithCadastralUnit_CountsOnlyObservationsAtItsLocations()
    {
        const string areaFid = "981920";
        await SeedAreaWithObservationsAtLocationsOneAndTwoAsync(areaFid);

        try
        {
            var result = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetAreaCountsAsync(2, new LocationSearchFilterDto
            {
                CadastralUnitIds = [CadastralUnitId],
            });

            result.Where(a => a.Fid == areaFid).Sum(a => a.ObservationCount).Should().Be(2);
        }
        finally
        {
            await RemoveAreaAsync(areaFid);
        }
    }

    /// <summary>
    /// Den valgte kommunen telles som uten matrikkelenhet, her det forhåndstalte antallet.
    /// Cellen eiendommen ligger i teller eiendommens observasjoner utenfor kommunen, og
    /// den ene som også er i kommunen telles ikke på nytt.
    /// </summary>
    [Fact]
    public async Task GetAreaCountsAsync_WithCadastralUnitAndMunicipality_AddsCadastralObservationsOutsideTheMunicipality()
    {
        const string municipalityFid = "981930";
        const string propertyAreaFid = "981935";
        await SeedAreaWithObservationsAtLocationsOneAndTwoAsync(propertyAreaFid);
        await RemoveAreaAsync(municipalityFid);
        SeedArea(municipalityFid, "Matrikkelenhet test valgt kommune", observationCount: 7);
        await _context.SaveChangesAsync();
        var inMunicipality = await SeedMunicipalityRowAsync(int.Parse(municipalityFid), _locationTwo);

        try
        {
            var atLocationTwo = await _context.Set<Observation>().CountAsync(o => o.LocationId == _locationTwo.Id);

            var result = await CreateRepositoryWithCadastralUnit(_locationTwo.Id).GetAreaCountsAsync(2, new LocationSearchFilterDto
            {
                MunicipalityIds = [municipalityFid],
                CadastralUnitIds = [CadastralUnitId],
            });

            result.Single(a => a.Fid == municipalityFid).ObservationCount.Should().Be(7);
            result.Single(a => a.Fid == propertyAreaFid).ObservationCount.Should().Be(atLocationTwo - 1);
        }
        finally
        {
            _context.Remove(inMunicipality);
            await RemoveAreaAsync(municipalityFid);
            await RemoveAreaAsync(propertyAreaFid);
        }
    }

    private SearchRepository CreateRepositoryWithCadastralUnit(params int[] locationIds) =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(),
            new StubCadastralUnitService(new() { [CadastralUnitId] = locationIds }));

    /// <summary>
    /// Et eget område med en indeksrad for hver observasjon på lokasjon 1 og 2. De seedede
    /// indeksradene til observasjonene røres ikke, siden lokasjonstestene leser dem.
    /// </summary>
    private async Task SeedAreaWithObservationsAtLocationsOneAndTwoAsync(string areaFid)
    {
        await RemoveAreaAsync(areaFid);

        var observations = await _context.Set<Observation>()
            .Where(o => o.LocationId == _locationOne.Id || o.LocationId == _locationTwo.Id)
            .ToListAsync();

        SeedArea(areaFid, $"Matrikkelenhet test kommune {areaFid}");
        _context.Set<ObservationEntityIndex>().AddRange(observations.Select(o => new ObservationEntityIndex
        {
            ObservationId = o.Id,
            EntityTypeId = TestAreaTypeMunicipalityId,
            EntityId = int.Parse(areaFid),
            TaxonGroupId = o.TaxonGroupId,
            BasisOfRecordId = o.BasisOfRecordId,
            RegistrationStatusId = o.RegistrationStatusId,
            LocationId = o.LocationId,
        }));
        await _context.SaveChangesAsync();
    }

    private async Task RemoveAreaAsync(string areaFid)
    {
        var areaEntityId = int.Parse(areaFid);
        _context.Set<ObservationEntityIndex>().RemoveRange(_context.Set<ObservationEntityIndex>()
            .Where(i => i.EntityTypeId == TestAreaTypeMunicipalityId && i.EntityId == areaEntityId));
        _context.Set<Area>().RemoveRange(_context.Set<Area>().Where(a => a.Fid == areaFid));
        await _context.SaveChangesAsync();
    }

    private async Task<ObservationEntityIndex> SeedMunicipalityRowAsync(int municipalityEntityId, Location location)
    {
        var observationId = await _context.Set<Observation>()
            .Where(o => o.LocationId == location.Id)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .FirstAsync();

        var indexRow = new ObservationEntityIndex
        {
            ObservationId = observationId,
            EntityTypeId = (int)ObservationIndexEntityType.Municipality,
            EntityId = municipalityEntityId,
            TaxonGroupId = TestObservationTaxonGroupOneId,
            BasisOfRecordId = TestBasisOfRecordOneId,
            RegistrationStatusId = (byte)RegistrationStatus.Found,
            LocationId = location.Id,
        };
        _context.Set<ObservationEntityIndex>().Add(indexRow);
        await _context.SaveChangesAsync();
        return indexRow;
    }

    private DbSet<Dictionary<string, object>> ObservationTags() =>
        _context.Set<Dictionary<string, object>>("ObservationTag");

    private async Task AddTagRowAsync(int observationId, TagEnum tag)
    {
        var tagId = (int)tag;
        if (!await _context.Set<Tag>().AnyAsync(t => t.Id == tagId))
            _context.Set<Tag>().Add(new Tag { Id = tagId, Name = tag.ToString() });
        ObservationTags().Add(new Dictionary<string, object>
        {
            ["ObservationId"] = observationId,
            ["TagId"] = tagId,
        });
        await _context.SaveChangesAsync();
        await OppdaterRegistreringsstatusAsync(observationId);
    }

    private async Task RemoveSyntheticTagRowAsync(int observationId, int tagId)
    {
        ObservationTags().RemoveRange(ObservationTags()
            .Where(ot => (int)ot["ObservationId"] == observationId && (int)ot["TagId"] == tagId));
        await _context.SaveChangesAsync();
        await OppdaterRegistreringsstatusAsync(observationId);
    }

    /// <summary>
    /// Holder den denormaliserte kolonnen i takt med taggene, slik seksjon C i
    /// BackfillAll gjør i produksjon: ikke gjenfunnet slår ikke funnet, og uten
    /// noen av dem er svaret funnet.
    ///
    /// Lokasjonssøket leser RegistrationStatusId fra indekstabellen og ikke fra
    /// ObservationTag — kolonnen finnes nettopp for å slippe joinen — så en test
    /// som bare legger på en tagg ville ellers ikke sett noen endring.
    ///
    /// Bare indeksraden oppdateres, som i seksjon C. Observation.RegistrationStatusId
    /// vedlikeholdes ikke derfra.
    /// </summary>
    private async Task OppdaterRegistreringsstatusAsync(int observationId)
    {
        var tagIds = await ObservationTags()
            .Where(ot => (int)ot["ObservationId"] == observationId)
            .Select(ot => (int)ot["TagId"])
            .ToListAsync();

        var status = tagIds.Contains((int)TagEnum.NotRecovered) ? (byte)RegistrationStatus.NotRecovered
            : tagIds.Contains((int)TagEnum.Absent) ? (byte)RegistrationStatus.Absent
            : (byte)RegistrationStatus.Found;

        var indexRows = await _context.Set<ObservationEntityIndex>()
            .Where(idx => idx.ObservationId == observationId)
            .ToListAsync();

        foreach (var row in indexRows)
            row.RegistrationStatusId = status;

        await _context.SaveChangesAsync();
    }

    private SearchRepository CreateCompleteFilterRepository() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

    private void SeedArea(string areaFid, string name, int observationCount = 0) =>
        _context.Set<Area>().Add(new Area
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = areaFid,
            Name = name,
            AreaTypeId = TestAreaTypeMunicipalityId,
            ZoomLevel = 2,
            ParentFid = "repo-parent",
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = observationCount,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
        });

    private void SeedIndexRow(
        int observationId,
        int areaEntityId,
        int? institutionOrgId = null,
        int? datasetOrgId = null,
        byte? behaviorId = null,
        bool changedByAnnotation = false,
        bool sensitive = false,
        bool uncertainIdentification = false,
        bool introduced = false) =>
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = observationId,
            EntityTypeId = TestAreaTypeMunicipalityId,
            EntityId = areaEntityId,
            TaxonGroupId = TestObservationTaxonGroupOneId,
            BasisOfRecordId = TestBasisOfRecordOneId,
            RegistrationStatusId = 0,
            InstitutionOrgId = institutionOrgId,
            DatasetOrgId = datasetOrgId,
            BehaviorId = behaviorId,
            ChangedByAnnotation = changedByAnnotation,
            Sensitive = sensitive,
            UncertainIdentification = uncertainIdentification,
            Introduced = introduced,
        });

    /// <summary>
    /// ProjectOrgId peker på Organization, som igjen peker på OrganizationType.
    /// Testdataene inneholder ingen av delene, så begge må opprettes.
    ///
    /// Id-ene settes IKKE eksplisitt: Organization.Id er en identity-kolonne, og et
    /// eksplisitt id-innslag avvises med IDENTITY_INSERT OFF. Vi lar databasen
    /// tildele dem og leser dem tilbake.
    /// </summary>
    private async Task<List<int>> CreateDatasetOrganizationsAsync(int count)
    {
        const string testTypeName = "Datasett (integrasjonstest)";

        var organizationType = await _context.Set<OrganizationType>()
            .FirstOrDefaultAsync(t => t.Name == testTypeName);

        if (organizationType is null)
        {
            organizationType = new OrganizationType
            {
                Name = testTypeName,
                Description = "Syntetisk organisasjonstype for integrasjonstester",
            };
            _context.Set<OrganizationType>().Add(organizationType);
            await _context.SaveChangesAsync();
        }

        var organizations = Enumerable.Range(0, count)
            .Select(i => new Organization
            {
                Name = $"Datasett integrasjonstest {i}",
                OrganizationTypeId = organizationType.Id,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
            })
            .ToList();

        _context.Set<Organization>().AddRange(organizations);
        await _context.SaveChangesAsync();

        return organizations.Select(o => o.Id).ToList();
    }
    private async Task RemoveSyntheticDatasetRowsAsync(int observationId)
    {
        _context.Set<ObservationProject>()
            .RemoveRange(_context.Set<ObservationProject>().Where(d => d.ObservationId == observationId));
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Fjerner bare radene en test selv la på ett syntetisk område, og lar
    /// observasjonens øvrige rader stå.
    ///
    /// Brukes når testen trenger en EKTE seedet observasjon — typisk fordi
    /// filteret krever en Observation-rad med en bestemt egenskap.
    /// <see cref="RemoveSyntheticRowsAsync"/> sletter da for mye: alle
    /// indeksradene OG taksonhierarkiet til en observasjon som resten av klassen
    /// regner med. Seedet bygges én gang og deles av alle testene her, så det
    /// kommer ikke tilbake — lokasjonstellingene faller stille med én.
    /// </summary>
    private async Task RemoveSyntheticAreaRowsAsync(int observationId, int areaEntityId, string areaFid)
    {
        _context.Set<ObservationEntityIndex>()
            .RemoveRange(_context.Set<ObservationEntityIndex>()
                .Where(i => i.ObservationId == observationId && i.EntityId == areaEntityId));
        _context.Set<Area>()
            .RemoveRange(_context.Set<Area>().Where(a => a.Fid == areaFid));
        await _context.SaveChangesAsync();
    }

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

        var observations = new[]
        {
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 1),
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 2),
            CreateObservation(_locationOne.Id, TestObservationTaxonGroupOneId, TestCategoryOneId, TestBasisOfRecordOneId, CollectionOne, 25, 3),
            CreateObservation(_locationTwo.Id, TestObservationTaxonGroupTwoId, TestCategoryTwoId, TestBasisOfRecordTwoId, CollectionTwo, 100, 4),
            CreateObservation(_locationTwo.Id, TestObservationTaxonGroupTwoId, TestCategoryTwoId, TestBasisOfRecordTwoId, CollectionTwo, 100, 5),
            CreateObservation(_locationThree.Id, TestObservationTaxonGroupOneId, TestCategoryTwoId, TestBasisOfRecordOneId, CollectionOne, 500, 6),
        };
        _context.Set<Observation>().AddRange(observations);

        _context.Set<Area>().AddRange(
            CreateArea("Repo kommune A", TestAreaTypeMunicipalityId, 10, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 10, 10),
            CreateArea("Repo gruppert kommune", TestAreaTypeMunicipalityId, 15, 10),
            CreateArea("Repo fylke A", TestAreaTypeCountyId, 20, 7),
            CreateArea("Repo felles område", TestAreaTypeMunicipalityId, 30, 10),
            CreateArea("Repo felles område", TestAreaTypeCountyId, 40, 7));

        await _context.SaveChangesAsync();

        // ObservationEntityIndex speiler BackfillAll seksjon C/D/E: én rad per
        // observasjon per område, med filterkolonnene kopiert fra observasjonen.
        //
        // Lokasjonssøket grupperer på ObservationEntityIndex.LocationId og rører ikke
        // Observation i det hele tatt — uten radene her returnerer GetLocationsAsync
        // tomt uansett hva som står i Observation. Filterkolonnene må være de samme,
        // ellers filtrerer testene på noe annet enn det de seeder.
        //
        // Legges til etter lagringen fordi Observation.Id er identity-generert.
        _context.Set<ObservationEntityIndex>().AddRange(observations.Select(o => new ObservationEntityIndex
        {
            ObservationId = o.Id,
            EntityTypeId = TestAreaTypeMunicipalityId,
            EntityId = TestLocationsAreaEntityId,
            TaxonGroupId = o.TaxonGroupId,
            CategoryId = o.CategoryId,
            BasisOfRecordId = o.BasisOfRecordId,
            CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters,
            DateTimeCollected = o.DateTimeCollected,
            // MonthCollected MÅ følge DateTimeCollected. I produksjon er den
            // backfillet og verifisert av migrasjon 20260920125459, og
            // byggeskriptene for tellebufferne nekter å bygge hvis de to spriker:
            // periodedimensjonen bøttes på MONTH(DateTimeCollected), mens
            // filterstiene leser MonthCollected. Et seed som bare satte datoen
            // ville derfor stoppe CountCacheScriptIntegrationTests.
            MonthCollected = (byte?)o.DateTimeCollected?.Month,
            RegistrationStatusId = o.RegistrationStatusId,
            HasMediaFiles = o.HasMediaFiles,
            BehaviorId = o.BehaviorId,
            DatasetOrgId = o.DatasetOrgId,
            LocationId = o.LocationId,
        }));

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

    private static Observation CreateObservation(int locationId, int taxonGroupId, int categoryId, int basisOfRecordId, int datasetOrgId, int coordinatePrecision, int hashCode)
        => new()
        {
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            NodeId = 1,
            DatasetOrgId = datasetOrgId,
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

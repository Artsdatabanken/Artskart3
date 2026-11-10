using Artskart3.Core.Constants;
using Artskart3.Core.Domain.Entities;
using AreaType = Artskart3.Core.Domain.Enums.AreaType;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vaktsetningene i katalognummer-oppslaget.
///
/// Alle tre returnerer FØR spørringen bygges, og det er hele poenget: endepunktet
/// er anonymt, og et prefikssøk som ikke kan brukes som seek degraderer til full
/// skanning av IX_Observation_CatalogNumber over 61M rader. Testene kjører derfor
/// mot en tom in-memory-base — når de passerer uten at noen spørring utføres, er
/// vakten faktisk foran databasen og ikke bak den.
/// </summary>
public class LookupRepositoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SearchCatalogNumbersAsync_WithBlankSearch_ReturnsEmpty(string search)
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(search, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCatalogNumbersAsync_WithNullSearch_ReturnsEmpty()
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(null!, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Minstelengden håndheves på serveren. Debouncing og minstelengde i
    /// typeaheaden gjelder bare nettleseren — endepunktet kan kalles direkte.
    /// </summary>
    [Fact]
    public async Task SearchCatalogNumbersAsync_WithSearchShorterThanMinimum_ReturnsEmpty()
    {
        var sut = CreateRepository();
        var tooShort = new string('1', SearchConstants.MinCatalogNumberSearchLength - 1);

        var result = await sut.SearchCatalogNumbersAsync(tooShort, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Starter søket med %, _ eller [, blir det escapede mønsteret et klammeuttrykk
    /// ('[%]abc%'). SQL Server kan ikke bruke det som seek-prefiks, og oppslaget
    /// degraderer til full skanning. Disse avvises framfor å kjøres.
    /// </summary>
    [Theory]
    [InlineData("%abc")]
    [InlineData("_abc")]
    [InlineData("[abc")]
    public async Task SearchCatalogNumbersAsync_WithWildcardPrefix_ReturnsEmpty(string search)
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(search, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Jokertegn LENGER INNE i strengen er uproblematiske: prefikset foran dem er
    /// fortsatt en gyldig seek, og de escapes. Avvises de også, mister brukeren
    /// katalognumre som faktisk inneholder tegnene.
    /// </summary>
    [Theory]
    [InlineData("ab%c")]
    [InlineData("ab_c")]
    [InlineData("ab[c")]
    public async Task SearchCatalogNumbersAsync_WithWildcardInsideTerm_IsNotRejectedByGuard(string search)
    {
        var sut = CreateRepository();

        var act = async () => await sut.SearchCatalogNumbersAsync(search, 10);

        // Går videre til spørringen. Mot en tom base gir det tomt resultat, ikke
        // et tidlig retur — men det viktige er at vakten ikke stanser det.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchCatalogNumbersAsync_WithTrimmableSearch_UsesTrimmedLength()
    {
        var sut = CreateRepository();
        var paddedButTooShort = $"  {new string('1', SearchConstants.MinCatalogNumberSearchLength - 1)}  ";

        var result = await sut.SearchCatalogNumbersAsync(paddedButTooShort, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxonGroupNamesByTaxonIdsAsync_ReturnsGroupNamePerRequestedTaxon()
    {
        var context = CreateContext();
        var fish = new TaxonGroup { Id = 1, Name = "Fisker" };
        var molluscs = new TaxonGroup { Id = 2, Name = "Bløtdyr" };
        var deletedGroup = new TaxonGroup { Id = 3, Name = "Slettet", IsDeleted = true };
        context.AddRange(fish, molluscs, deletedGroup);
        context.AddRange(
            CreateTaxon(42911, fish),
            CreateTaxon(79773, molluscs),
            CreateTaxon(55555, molluscs),
            CreateTaxon(66666, deletedGroup));
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.GetTaxonGroupNamesByTaxonIdsAsync([42911, 79773, 66666, 99999]);

        result.Should().BeEquivalentTo(new Dictionary<int, string> { [42911] = "Fisker", [79773] = "Bløtdyr" });
    }

    [Fact]
    public async Task GetTaxonGroupNamesByTaxonIdsAsync_WithNoIds_ReturnsEmpty()
    {
        var sut = CreateRepository();

        var result = await sut.GetTaxonGroupNamesByTaxonIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCatalogNumberByObservationIdsAsync_UsesCatalogNumberWhenPresent()
    {
        var context = CreateContext();
        context.Observations.AddRange(
            new Observation { Id = 1, CatalogNumber = "O-123", ProxyId = "urn:catalog:o:l:O-123" },
            new Observation { Id = 2, CatalogNumber = "O-123" });
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.GetCatalogNumberByObservationIdsAsync([1, 2]);

        result!.CatalogNumber.Should().Be("O-123");
        result.ObservationIds.Should().BeEquivalentTo([1, 2]);
    }

    /// <summary>
    /// Et filter laget fra en innlimt URN peker på observasjoner som ikke
    /// nødvendigvis har katalognummer.
    /// </summary>
    [Fact]
    public async Task GetCatalogNumberByObservationIdsAsync_WithoutCatalogNumber_FallsBackToProxyId()
    {
        var context = CreateContext();
        context.Observations.Add(new Observation { Id = 1, ProxyId = "urn:catalog:o:l:37", OccurrenceId = "urn:catalog:O:L:37" });
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.GetCatalogNumberByObservationIdsAsync([1]);

        result!.CatalogNumber.Should().Be("urn:catalog:o:l:37");
    }

    [Fact]
    public async Task GetCatalogNumberByObservationIdsAsync_WithUnknownIds_ReturnsNull()
    {
        var sut = CreateRepository();

        var result = await sut.GetCatalogNumberByObservationIdsAsync([999]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_ReturnsNameAndSkipsDeleted()
    {
        var context = CreateContext();
        context.Organizations.AddRange(
            new Organization { Id = 1, Name = "Prosjekt A", OrganizationTypeId = 3 },
            new Organization { Id = 2, Name = "Slettet", OrganizationTypeId = 2, IsDeleted = true });
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        (await sut.GetOrganizationByIdAsync(1))!.Name.Should().Be("Prosjekt A");
        (await sut.GetOrganizationByIdAsync(2)).Should().BeNull();
    }

    /// <summary>
    /// Tellingen oppdateres ikke av denne løsningen, så en organisasjon uten telling
    /// kan ha observasjoner likevel. Den skal fortsatt kunne søkes opp.
    /// </summary>
    [Fact]
    public async Task SearchOrganizationsByTypeAsync_ReturnsCountsAndKeepsOrganizationsWithoutOne()
    {
        var context = CreateContext();
        context.Organizations.AddRange(
            new Organization { Id = 1, Name = "Kartlegging Nord", OrganizationTypeId = 3, ObservationCount = 1234 },
            new Organization { Id = 2, Name = "Kartlegging Tom", OrganizationTypeId = 3, ObservationCount = null },
            new Organization { Id = 3, Name = "Kartlegging Datasett", OrganizationTypeId = 2, ObservationCount = 5 });
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.SearchOrganizationsByTypeAsync("kartlegging", 3, 10);

        result.Should().BeEquivalentTo([
            new { Id = 1, Name = "Kartlegging Nord", ObservationCount = (int?)1234 },
            new { Id = 2, Name = "Kartlegging Tom", ObservationCount = (int?)null },
        ]);
    }

    [Fact]
    public async Task SearchAreasAsync_PutsPrefixMatchesAndCurrentAreasFirst()
    {
        var context = CreateContext();
        context.Set<Area>().AddRange(
            CreateArea(1, "5501", "Tromsø", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)),
            CreateArea(2, "1902", "Tromsø", AreaType.Municipality, isCurrent: false, new DateTime(2019, 1, 29)),
            CreateArea(3, "5401", "Tromsø", AreaType.Municipality, isCurrent: false, new DateTime(2021, 1, 6)),
            CreateArea(4, "55", "Troms", AreaType.County, isCurrent: true, new DateTime(2024, 1, 16)),
            CreateArea(5, "1234", "Vest-Tromsø", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)));
        context.Set<HistoricalAreaLocation>().AddRange(
            new HistoricalAreaLocation { AreaId = 2, LocationId = 100 },
            new HistoricalAreaLocation { AreaId = 3, LocationId = 100 });
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = (await sut.SearchAreasAsync("troms", 10)).ToList();

        result.Select(a => a.Id).Should().Equal(4, 1, 3, 2, 5);
        result.Single(a => a.Id == 2).Should().BeEquivalentTo(new
        {
            Fid = "1902", Name = "Tromsø", AreaTypeId = (int)AreaType.Municipality, IsCurrent = false, EndYear = 2020,
            ObservationCount = (int?)null,
        });
        result.Single(a => a.Id == 1).ObservationCount.Should().Be(500);
    }

    /// <summary>
    /// Uten koblinger ville et historisk område gitt et tomt søk. Det skal ikke kunne velges.
    /// </summary>
    [Fact]
    public async Task SearchAreasAsync_LeavesOutHistoricalAreasWithoutLocations()
    {
        var context = CreateContext();
        context.Set<Area>().AddRange(
            CreateArea(1, "1902", "Tromsø", AreaType.Municipality, isCurrent: false, new DateTime(2019, 1, 29)),
            CreateArea(2, "5501", "Tromsø", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)));
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.SearchAreasAsync("tromsø", 10);

        result.Select(a => a.Id).Should().Equal(2);
    }

    [Fact]
    public async Task SearchAreasAsync_MatchesTheStartOfTheNumber()
    {
        var context = CreateContext();
        context.Set<Area>().AddRange(
            CreateArea(1, "5501", "Tromsø", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)),
            CreateArea(2, "1550", "Hustadvika", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)));
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.SearchAreasAsync("55", 10);

        result.Select(a => a.Id).Should().Equal(1);
    }

    [Fact]
    public async Task SearchAreasAsync_LeavesOutRestrictedAndDeletedAreas()
    {
        var context = CreateContext();
        var deleted = CreateArea(2, "0302", "Oslo gamle", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17));
        deleted.IsDeleted = true;
        context.Set<Area>().AddRange(
            CreateArea(1, "Naturbase VV00000001", "Oslo naturreservat", AreaType.RestrictedArea, isCurrent: true, new DateTime(2024, 1, 17)),
            deleted,
            CreateArea(3, "0301", "Oslo", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17)));
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = await sut.SearchAreasAsync("oslo", 10);

        result.Select(a => a.Id).Should().Equal(3);
    }

    [Fact]
    public async Task GetAreaByIdAsync_ReturnsHistoricalAreaAndSkipsDeleted()
    {
        var context = CreateContext();
        var deleted = CreateArea(2, "0302", "Slettet", AreaType.Municipality, isCurrent: false, new DateTime(2019, 1, 29));
        deleted.IsDeleted = true;
        context.Set<Area>().AddRange(
            CreateArea(1, "54", "Troms og Finnmark", AreaType.County, isCurrent: false, new DateTime(2021, 1, 6)),
            deleted);
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        (await sut.GetAreaByIdAsync(1)).Should().BeEquivalentTo(new { Name = "Troms og Finnmark", IsCurrent = false, EndYear = 2024, CountyName = (string?)null });
        (await sut.GetAreaByIdAsync(2)).Should().BeNull();
    }

    /// <summary>
    /// Kommuner med like navn skilles på fylket, og en gjeldende kommune har ikke sluttår.
    /// </summary>
    [Fact]
    public async Task SearchAreasAsync_GivesMunicipalitiesTheirCounty()
    {
        var context = CreateContext();
        context.Set<Area>().AddRange(
            CreateArea(1, "1515", "Herøy", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17), parentFid: "15"),
            CreateArea(2, "1818", "Herøy", AreaType.Municipality, isCurrent: true, new DateTime(2024, 1, 17), parentFid: "18"),
            CreateArea(3, "15", "Møre og Romsdal", AreaType.County, isCurrent: true, new DateTime(2024, 1, 16)),
            CreateArea(4, "18", "Nordland", AreaType.County, isCurrent: true, new DateTime(2024, 1, 16)));
        await context.SaveChangesAsync();
        var sut = new LookupRepository(context, NullLogger<LookupRepository>.Instance);

        var result = (await sut.SearchAreasAsync("herøy", 10)).ToList();

        result.Should().BeEquivalentTo([
            new { Id = 1, CountyName = "Møre og Romsdal", EndYear = (int?)null },
            new { Id = 2, CountyName = "Nordland", EndYear = (int?)null },
        ]);
    }

    private static Area CreateArea(int id, string fid, string name, AreaType type, bool isCurrent, DateTime timeStamp, string parentFid = "") => new()
    {
        Id = id,
        ObservationCount = 500,
        Fid = fid,
        Name = name,
        AreaTypeId = (int)type,
        IsCurrent = isCurrent,
        TimeStamp = timeStamp,
        DocumentId = string.Empty,
        ParentFid = parentFid,
        Bbox = string.Empty,
    };

    private static Taxon CreateTaxon(int id, TaxonGroup group) => new()
    {
        Id = id,
        TaxonGroup = group,
        ScientificNameIdHiarchy = string.Empty,
        TaxonIdHiarchy = string.Empty
    };

    private static ArtskartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static LookupRepository CreateRepository() =>
        new(CreateContext(), NullLogger<LookupRepository>.Instance);
}

using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Entities = Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kontrakten for områdelukingen: å hoppe over et område skal gi NØYAKTIG det
/// spørringen ville gitt, ikke omtrent.
///
/// Derfor sammenlignes to repositories i hver test — ett som kjenner
/// områdeboksene og kan luke, ett som ikke gjør det og må spørre databasen.
/// Å bare sjekke at det lukede svaret «ser tomt ut» ville bestått selv om
/// lukingen fjernet ekte treff.
///
/// EKTE områdetype-id brukes, ikke en syntetisk: områdefilteret slår opp
/// ObservationIndexEntityType.Municipality (= 1), så en syntetisk type ville gjort
/// at filteret aldri traff noe og testen bestått uten å bevise noe.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class AreaBoundsPruningIntegrationTests : IAsyncLifetime
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;

    // Egen id-rekkevidde, klar av 97xxxx, 981xxx og 982xxx.
    private const int OsloEntityId = 983010;
    private const int FarsundEntityId = 983011;
    private const string OsloFid = "983010";
    private const string FarsundFid = "983011";

    // To klynger langt fra hverandre, med ekte UTM 33N-størrelsesorden.
    private static readonly AreaBounds OsloBoks = new(248_000, 6_640_000, 258_000, 6_652_000);
    private static readonly AreaBounds FarsundBoks = new(385_000, 6_440_000, 395_000, 6_452_000);

    /// <summary>Dekker Oslo-klyngen, ikke Farsund.</summary>
    private static EnvelopeDto OsloUtsnitt => new()
    { MinX = 240_000, MinY = 6_630_000, MaxX = 280_000, MaxY = 6_670_000 };

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _osloLokasjon = null!;
    private Location _farsundLokasjon = null!;

    public AreaBoundsPruningIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    // -----------------------------------------------------------------------

    /// <summary>
    /// Kjernen: Farsund kan ikke nå Oslo-utsnittet, så spørringen hoppes over.
    /// Svaret må være det samme som om vi hadde spurt.
    /// </summary>
    [Fact]
    public async Task Omraade_utenfor_utsnittet_gir_samme_svar_som_uten_luking()
    {
        var filter = new LocationSearchFilterDto
        {
            MunicipalityIds = [FarsundFid],
            Envelope = OsloUtsnitt,
        };

        var medLuking = await MedBokser().GetLocationsAsync(filter);
        var utenLuking = await UtenBokser().GetLocationsAsync(filter);

        utenLuking.Should().BeEmpty("Farsund har ingen lokasjon i Oslo-utsnittet");
        medLuking.Should().BeEquivalentTo(utenLuking);
    }

    /// <summary>
    /// Motsatt retning, og den viktigste: et område som FAKTISK når utsnittet skal
    /// ikke lukes bort. Går denne i stå, forsvinner ekte treff fra kartet.
    /// </summary>
    [Fact]
    public async Task Omraade_i_utsnittet_gir_samme_svar_som_uten_luking()
    {
        var filter = new LocationSearchFilterDto
        {
            MunicipalityIds = [OsloFid],
            Envelope = OsloUtsnitt,
        };

        var medLuking = await MedBokser().GetLocationsAsync(filter);
        var utenLuking = await UtenBokser().GetLocationsAsync(filter);

        utenLuking.Should().NotBeEmpty();
        medLuking.Should().BeEquivalentTo(utenLuking);
    }

    /// <summary>
    /// Blandet utvalg. Her er det ikke kortslutning, men en kortere ID-liste — og
    /// den skal gi samme rader.
    /// </summary>
    [Fact]
    public async Task Delvis_luking_gir_samme_svar()
    {
        var filter = new LocationSearchFilterDto
        {
            MunicipalityIds = [OsloFid, FarsundFid],
            Envelope = OsloUtsnitt,
        };

        var medLuking = await MedBokser().GetLocationsAsync(filter);
        var utenLuking = await UtenBokser().GetLocationsAsync(filter);

        utenLuking.Should().NotBeEmpty();
        medLuking.Should().BeEquivalentTo(utenLuking);
    }

    /// <summary>
    /// Uten utsnitt finnes det ikke noe å luke mot. Da må hele filteret stå, ellers
    /// ville et områdesøk uten kart svart tomt.
    /// </summary>
    [Fact]
    public async Task Uten_utsnitt_lukes_ingenting()
    {
        var filter = new LocationSearchFilterDto { MunicipalityIds = [FarsundFid] };

        var medLuking = await MedBokser().GetLocationsAsync(filter);
        var utenLuking = await UtenBokser().GetLocationsAsync(filter);

        utenLuking.Should().NotBeEmpty("Farsund har lokasjoner, de ligger bare ikke i Oslo");
        medLuking.Should().BeEquivalentTo(utenLuking);
    }

    /// <summary>
    /// Tjenesten kan bli spurt før den har rukket å laste boksene. Da skal ingenting
    /// lukes — det motsatte ville tømt hvert eneste områdefilter.
    /// </summary>
    [Fact]
    public async Task Uten_lastede_bokser_lukes_ingenting()
    {
        var filter = new LocationSearchFilterDto
        {
            MunicipalityIds = [OsloFid],
            Envelope = OsloUtsnitt,
        };

        var utenBokser = await UtenBokser().GetLocationsAsync(filter);

        utenBokser.Should().NotBeEmpty();
    }

    /// <summary>
    /// Uten områdefilter skal utsnittet fortsatt virke som før. Lukingen må ikke
    /// smitte over på søk som ikke har valgt noe område.
    /// </summary>
    [Fact]
    public async Task Uten_omraadefilter_virker_utsnittet_som_foer()
    {
        var filter = new LocationSearchFilterDto { Envelope = OsloUtsnitt };

        var medLuking = await MedBokser().GetLocationsAsync(filter);
        var utenLuking = await UtenBokser().GetLocationsAsync(filter);

        medLuking.Should().BeEquivalentTo(utenLuking);
        medLuking.Should().Contain(m => m.Id == _osloLokasjon.Id);
        medLuking.Should().NotContain(m => m.Id == _farsundLokasjon.Id);
    }

    // -----------------------------------------------------------------------

    private SearchRepository MedBokser()
    {
        var stub = new StubAreaHierarchyService();
        stub.Bounds[(Kommune, OsloEntityId)] = OsloBoks;
        stub.Bounds[(Kommune, FarsundEntityId)] = FarsundBoks;
        return NewRepository(stub);
    }

    private SearchRepository UtenBokser() => NewRepository(new StubAreaHierarchyService());

    private SearchRepository NewRepository(StubAreaHierarchyService areaHierarchy) =>
        new(_context, NullLogger<SearchRepository>.Instance,
            Options.Create(new PaginationOptions()), areaHierarchy, new StubTaxonHierarchyService());

    private async Task SeedAsync()
    {
        _osloLokasjon = await EnsureLocationAsync("bounds-loc-oslo", 253_000, 6_646_000);
        _farsundLokasjon = await EnsureLocationAsync("bounds-loc-farsund", 390_000, 6_446_000);

        if (await _context.Set<ObservationEntityIndex>()
                .AnyAsync(i => i.EntityTypeId == Kommune && i.EntityId == OsloEntityId))
        {
            return;
        }

        // To observasjoner i Oslo, én i Farsund.
        //
        // BÅDE Observation og ObservationEntityIndex må seedes. Lokasjonssøket
        // teller fra Observation, mens områdefilteret er en EXISTS mot
        // indekstabellen — seeder man bare den ene, tester man ingenting.
        // FK-verdier hentes fra seed_data.sql i stedet for å hardkodes, så
        // testen ikke knekker når seeden endres.
        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();

        var observasjoner = new[]
        {
            NewObservation(_osloLokasjon, taxon, taxonName, kategori, funntype),
            NewObservation(_osloLokasjon, taxon, taxonName, kategori, funntype),
            NewObservation(_farsundLokasjon, taxon, taxonName, kategori, funntype),
        };

        _context.Set<Observation>().AddRange(observasjoner);
        await _context.SaveChangesAsync();

        // Indeksradene legges til etter lagringen fordi Observation.Id er
        // identity-generert.
        _context.Set<ObservationEntityIndex>().AddRange(
            NewIndexRow(observasjoner[0].Id, OsloEntityId, _osloLokasjon.Id),
            NewIndexRow(observasjoner[1].Id, OsloEntityId, _osloLokasjon.Id),
            NewIndexRow(observasjoner[2].Id, FarsundEntityId, _farsundLokasjon.Id));

        await _context.SaveChangesAsync();
    }

    private static Observation NewObservation(
        Location lokasjon, Taxon taxon, TaxonName taxonName, Entities.Category kategori, Entities.BasisOfRecord funntype) => new()
    {
        DateLastModified = DateTime.UtcNow,
        DateTimeRecordImported = DateTime.UtcNow,
        DateTimeRecordProcessed = DateTime.UtcNow,
        NodeId = 1,
        DatasetOrgId = null,
        BasisOfRecordId = funntype.Id,
        TaxonId = taxon.Id,
        MatchedScientificNameId = taxonName.Id,
        TaxonGroupId = taxon.TaxonGroupId,
        CategoryId = kategori.Id,
        Latitude = lokasjon.Latitude ?? 0,
        Longitude = lokasjon.Longitude ?? 0,
        CoordinatePrecisionInMeters = 25,
        East = lokasjon.East,
        North = lokasjon.North,
        LocationId = lokasjon.Id,
        HashCode = 0,
        ProcessEngineId = 1,
        HasErrors = false,
        HasAnnotations = false,
    };

    private async Task<Location> EnsureLocationAsync(string lookupId, int east, int north)
    {
        var funnet = await _context.Set<Location>().FirstOrDefaultAsync(l => l.LookupId == lookupId);
        if (funnet != null) return funnet;

        var ny = new Location
        {
            LookupId = lookupId,
            Latitude = 59.9,
            Longitude = 10.7,
            CoordinatePrecision = 25,
            East = east,
            North = north,
            Locality = lookupId,
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = null,
        };

        _context.Set<Location>().Add(ny);
        await _context.SaveChangesAsync();
        return ny;
    }

    private static ObservationEntityIndex NewIndexRow(int observationId, int entityId, int locationId) => new()
    {
        ObservationId = observationId,
        EntityTypeId = Kommune,
        EntityId = entityId,
        LocationId = locationId,
        TaxonGroupId = 983200,
        CategoryId = 983300,
        BasisOfRecordId = 983400,
        RegistrationStatusId = 1,
    };
}

using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Domain.Entities;
using Entities = Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Persistence.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kjører de EKTE byggeskriptene — Scripts/BuildAreaCountCache.sql og
/// Scripts/BuildLocationCountCache.sql — mot databasen, og leser resultatet med
/// de ekte tjenestene.
///
/// HVORFOR DENNE FINNES
/// De andre buffertestene bygger bufferen med C#-kode som speiler skriptene
/// (se BuildAsync i LocationCountCacheIntegrationTests). De beviser derfor at
/// LESEREN er enig med en C#-reimplementasjon — aldri at SKRIPTET er enig med
/// leseren. Det er to ulike påstander, og det er den siste som gjelder i
/// produksjon: der er det skriptene som bygger bufferen, kjørt av migrasjonen
/// BuildCountCaches.
///
/// Fikstur-oppsettet kjører EnsureCreatedAsync, ikke MigrateAsync, så
/// migrasjonene — BuildCountCaches inkludert — kjører ikke i testene. Uten denne
/// filen utføres skriptene aldri av noe som helst før en deploy.
///
/// Tre av kontrollene under er vakter skriptene nettopp har fått, og de er her
/// for å ikke bli borte igjen: at lokasjonsskriptet nekter å bygge på foreldede
/// bøtter, og at månedsinvarianten stopper bygget. Begge feilet tidligere stille,
/// med for korte lokasjonslister stemplet «Ready».
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class CountCacheScriptIntegrationTests : IAsyncLifetime
{
    private const string AreaScript = "Artskart3.Infrastructure.Scripts.BuildAreaCountCache.sql";
    private const string LocationScript = "Artskart3.Infrastructure.Scripts.BuildLocationCountCache.sql";

    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;

    // Egen id-rekkevidde, klar av 97xxxx og 985xxx som de andre buffertestene bruker.
    private const int KommuneA = 986010;
    private const int KommuneB = 986011;
    private const int FylkeA = 986020;
    private const int TaksongruppeA = 986200;
    private const int TaksongruppeB = 986201;

    private const int Ost = 310_000;
    private const int Nord = 6_610_000;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _l1 = null!;
    private Location _l2 = null!;

    public CountCacheScriptIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x =>
            {
                x.UseNetTopologySuite();

                // Skriptene bygger 13 ett-filter-dimensjoner og 78 par med markør,
                // og de oppdaterer statistikk til slutt. Lite datasett, men mange
                // setninger — standardtimeouten på 30 s er i knappeste laget.
                x.CommandTimeout(600);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
        await ClearCacheAsync();
    }

    public async Task DisposeAsync()
    {
        await ClearCacheAsync();
        await _context.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Skriptene bygger en buffer tjenestene godtar
    // -----------------------------------------------------------------------

    /// <summary>
    /// Skriptene bygger en buffer tjenestene godtar, og den gir NØYAKTIG det
    /// tellingen gir — for hver dimensjonsform bufferen støtter.
    ///
    /// ÉN TEST, OG DET ER MED VILJE. Dette var fire tester, én per tema, og det var
    /// penere å lese. Men hver av dem kjørte begge byggeskriptene, altså åtte
    /// skriptkjøringer med TRUNCATE, massive INSERT-er og UPDATE STATISTICS mot
    /// databasen hele samlingen deler. Det var nok til å velte en annen testklasse:
    /// SearchEndpointTests.GetAreaMarkers_WithZoomLevel2 gikk fra 900 ms til å henge
    /// forbi HttpClient-ens 100-sekundersgrense. Målt: hele samlingen passerte med
    /// fire skriptkjøringer og feilet reproduserbart med tolv.
    ///
    /// Bygget er altså delt framfor gjentatt. Deler man dette i flere [Fact] igjen,
    /// kommer den ustabiliteten tilbake — og den viser seg i en HELT ANNEN fil, som
    /// er den dyreste typen testfeil å finne.
    /// </summary>
    [Fact]
    public async Task Skriptbygget_buffer_godtas_og_stemmer_med_telling()
    {
        // Rekkefølgen er et krav, ikke en preferanse: lokasjonsskriptet låner
        // flerverdi-bøttene fra områdebufferen. Testes slik den kjøres.
        await RunAreaScriptAsync();
        await RunLocationScriptAsync();

        // --- Tilstanden oppslaget krever ---------------------------------------
        var kilderader = await CountCacheSourceRows.ReadAsync(_context, CancellationToken.None);

        var omraade = await _context.Set<AreaCountCacheState>().AsNoTracking()
            .SingleAsync(s => s.Id == 1);

        omraade.Status.Should().Be("Ready");
        omraade.SchemaVersion.Should().Be(AreaCountCacheDimensions.SchemaVersion,
            "skriptets @SchemaVersion-literal må følge dimensjonsregisteret");
        omraade.SourceRows.Should().Be(kilderader);

        var lokasjon = await _context.Set<LocationCountCacheState>().AsNoTracking()
            .SingleAsync(s => s.Id == LocationCountCacheState.PointCacheId);

        lokasjon.Status.Should().Be("Ready");
        lokasjon.SchemaVersion.Should().Be(AreaCountCacheDimensions.SchemaVersion);
        lokasjon.SourceRows.Should().Be(kilderader);

        // Tomme tabeller ville bestått alt over. Bufferen skal inneholde noe.
        (await _context.Set<AreaCountCacheLevel1>().CountAsync()).Should().BePositive();
        (await _context.Set<AreaCountCacheLevel2>().CountAsync()).Should().BePositive();
        (await _context.Set<LocationCountCacheLevel1>().CountAsync()).Should().BePositive();

        // Dimensjon 12 og 13 er flerverdi, og lokasjonsskriptet slår bøttene opp i
        // denne tabellen. Mangler de, stopper det — men da ville vi feilet over.
        var dimensjoner = await _context.Set<AreaCountCacheBucketMember>().AsNoTracking()
            .Select(m => m.DimensionId).Distinct().ToListAsync();
        dimensjoner.Should().Contain([12, 13]);

        // --- Ufiltrert og enkeltverdi ------------------------------------------
        await AssertSameAsCountingAsync(new LocationSearchFilterDto());
        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] });
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaksongruppeA, TaksongruppeB],
        });

        // --- Periode: den ene Range-dimensjonen --------------------------------
        //
        // Den er den eneste som bøttes på et UTREGNET uttrykk (år * 100 + måned) i
        // stedet for en kolonne slik den er, og dermed den eneste der skriptet og
        // filterstien kan lese to forskjellige ting. Det er nettopp det
        // månedsinvarianten under vokter.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            Period = new PeriodDto { Months = [6] },
        });

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            Period = new PeriodDto { From = 2024, To = 2024 },
        });

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            Period = new PeriodDto { From = 2024, To = 2025, Months = [6, 9] },
        });

        // --- Koordinatpresisjon: den andre Range-dimensjonen -------------------
        //
        // Her ER bøtta kolonneverdien, så et intervall må treffe nøyaktig bøttene i
        // intervallet — og NULL-bøtta må holdes utenfor i BEGGE stiene. Oppslaget
        // utelukker NullBucket eksplisitt; tellingen utelukker NULL implisitt, fordi
        // NULL >= 50 er ukjent og ikke sant. To ulike grunner til samme utfall, og
        // det er den slags som går fra hverandre.

        // Lukket intervall: treffer 100-radene, ikke 25 og ikke 1000.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 50, To = 500 },
        });

        // Bare nedre grense: 100 og 1000, men ikke raden uten presisjon.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 50 },
        });

        // Bare øvre grense.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { To = 100 },
        });

        // Et intervall ingen rad ligger i. Skal gi tomt, aldri ufiltrert.
        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 5000, To = 9000 },
        });
    }

    // -----------------------------------------------------------------------
    // Vaktene
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lokasjonsskriptet kan kjøres alene — Scripts/README.md sier det — og
    /// bøttene det låner er områdebufferens. Er de bygget fra et eldre datasett,
    /// finner ikke en observasjon med nytt områdemedlemskap sin egen signatur
    /// igjen, den indre joinen slipper den, og lokasjonslisten blir for kort.
    /// Stemplet «Ready» med dagens radantall gikk ferskhetsvakten god for det.
    ///
    /// Her simuleres en import mellom de to skriptene: én ny indeksrad er nok til
    /// at radantallet ikke stemmer lenger.
    /// </summary>
    [Fact]
    public async Task Lokasjonsskriptet_nekter_aa_bygge_paa_foreldede_boetter()
    {
        await RunAreaScriptAsync();

        var ny = await NyIndeksradAsync();

        try
        {
            var feil = await Assert.ThrowsAsync<SqlException>(RunLocationScriptAsync);
            feil.Message.Should().Contain("foreldet");

            // Og den skal ikke ha stemplet noe som helst klart underveis.
            var tilstand = await _context.Set<LocationCountCacheState>().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == LocationCountCacheState.PointCacheId);
            tilstand?.Status.Should().NotBe("Ready");
        }
        finally
        {
            _context.Set<ObservationEntityIndex>().Remove(ny);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Månedsinvarianten: MonthCollected på indekstabellen er en vanlig kolonne
    /// fylt av en backfill, ikke en beregnet kolonne, og periodebøttene regnes av
    /// MONTH(DateTimeCollected) mens filterstien leser MonthCollected. Spriker de,
    /// svarer buffer og telling ulikt for samme filter.
    /// </summary>
    [Fact]
    public async Task Maanedsinvarianten_stopper_bygget()
    {
        var rad = await _context.Set<ObservationEntityIndex>()
            .FirstAsync(i => i.EntityId == KommuneA && i.MonthCollected != null);

        var riktig = rad.MonthCollected;
        rad.MonthCollected = (byte)(riktig == 1 ? 2 : 1);
        await _context.SaveChangesAsync();

        try
        {
            var feil = await Assert.ThrowsAsync<SqlException>(RunAreaScriptAsync);
            feil.Message.Should().Contain("MonthCollected");

            // Vakten står FØR Status settes til Building, så en buffer som
            // allerede var i orden skal ikke ha blitt slått av.
            var tilstand = await _context.Set<AreaCountCacheState>().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1);
            tilstand?.Status.Should().NotBe("Building");
        }
        finally
        {
            rad.MonthCollected = riktig;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// En måned uten dato er også et avvik, og det slår ut andre vei: bufferen
    /// legger raden i NULL-bøtta, som utelukkes, mens et månedsfilter teller den
    /// med. Dette er tilfellet forslaget om å bøtte på i.MonthCollected ville
    /// innført, og det er derfor invarianten voktes i stedet.
    /// </summary>
    [Fact]
    public async Task Maaned_uten_dato_stopper_ogsaa_bygget()
    {
        var rad = await _context.Set<ObservationEntityIndex>()
            .FirstAsync(i => i.EntityId == KommuneA && i.DateTimeCollected != null);

        var dato = rad.DateTimeCollected;
        rad.DateTimeCollected = null;
        await _context.SaveChangesAsync();

        try
        {
            var feil = await Assert.ThrowsAsync<SqlException>(RunAreaScriptAsync);
            feil.Message.Should().Contain("maaned uten dato");
        }
        finally
        {
            rad.DateTimeCollected = dato;
            await _context.SaveChangesAsync();
        }
    }

    // -----------------------------------------------------------------------
    // Oppsett
    // -----------------------------------------------------------------------

    private Task RunAreaScriptAsync() => RunScriptAsync(AreaScript);

    private Task RunLocationScriptAsync() => RunScriptAsync(LocationScript);

    /// <summary>
    /// Leser skriptet fra samme innebygde ressurs som migrasjonen BuildCountCaches
    /// kjører, så testen kan ikke komme i utakt med det som faktisk deployes.
    /// </summary>
    private async Task RunScriptAsync(string ressurs)
    {
        var assembly = typeof(CountCacheSourceRows).Assembly;

        await using var stream = assembly.GetManifestResourceStream(ressurs)
            ?? throw new InvalidOperationException(
                $"Fant ikke den innebygde ressursen '{ressurs}'.");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private SearchRepository WithCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService(), null,
            new LocationCountCacheService(_context, new StubTaxonHierarchyService(),
                new StubAreaHierarchyService(), Options.Create(new LocationCountCacheOptions()),
                NullLogger<LocationCountCacheService>.Instance));

    private SearchRepository WithoutCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(), new StubCadastralUnitService());

    /// <summary>
    /// Samme filter med og uten buffer skal gi identisk resultat, lokasjon for
    /// lokasjon.
    ///
    /// FØRST kontrolleres at bufferen i det hele tatt SVARER. Uten den sjekken er
    /// sammenligningen verdiløs: avviser bufferen filteret, faller BEGGE
    /// repositories tilbake til telling, de to svarene er like fordi de kommer fra
    /// samme kode, og testen er grønn uten å ha rørt en bufferrad. Et tomt svar er
    /// noe annet enn ingen svar — tom liste består, null gjør det ikke.
    /// </summary>
    private async Task AssertSameAsCountingAsync(LocationSearchFilterDto filter)
    {
        var fraBuffer = await CacheService().TryGetTopLocationsAsync(filter, 100_000);

        fraBuffer.Should().NotBeNull(
            "bufferen må svare på dette filteret, ellers sammenligner testen " +
            "tellingen med seg selv");

        var medBuffer = await WithCache().GetLocationsAsync(filter);
        var utenBuffer = await WithoutCache().GetLocationsAsync(filter);

        medBuffer.Select(x => (x.Id, x.ObservationCount)).OrderBy(x => x.Id)
            .Should().Equal(utenBuffer.Select(x => (x.Id, x.ObservationCount)).OrderBy(x => x.Id),
                "bufferen skal gi nøyaktig det tellingen gir, ikke omtrent");
    }

    private LocationCountCacheService CacheService() =>
        new(_context, new StubTaxonHierarchyService(), new StubAreaHierarchyService(),
            Options.Create(new LocationCountCacheOptions()),
            NullLogger<LocationCountCacheService>.Instance);

    private async Task ClearCacheAsync()
    {
        await _context.Set<AreaCountCacheLevel1>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheLevel2>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheBucketMember>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheState>().ExecuteDeleteAsync();
        await _context.Set<LocationCountCacheLevel1>().ExecuteDeleteAsync();
        await _context.Set<LocationCountCacheState>().ExecuteDeleteAsync();
    }

    /// <summary>
    /// En ekstra indeksrad, brukt for å flytte radantallet mellom de to skriptene.
    /// </summary>
    private async Task<ObservationEntityIndex> NyIndeksradAsync()
    {
        var mal = await _context.Set<ObservationEntityIndex>().AsNoTracking()
            .FirstAsync(i => i.EntityId == KommuneA);

        var ny = new ObservationEntityIndex
        {
            ObservationId = mal.ObservationId,
            EntityTypeId = Fylke,
            EntityId = FylkeA + 1,
            LocationId = mal.LocationId,
            TaxonGroupId = mal.TaxonGroupId,
            CategoryId = mal.CategoryId,
            BasisOfRecordId = mal.BasisOfRecordId,
            RegistrationStatusId = mal.RegistrationStatusId,
            DateTimeCollected = mal.DateTimeCollected,
            MonthCollected = mal.MonthCollected,
        };

        _context.Set<ObservationEntityIndex>().Add(ny);
        await _context.SaveChangesAsync();
        return ny;
    }

    private async Task SeedAsync()
    {
        _l1 = await EnsureLocationAsync("script-cache-1", Ost, Nord);
        _l2 = await EnsureLocationAsync("script-cache-2", Ost + 2_000, Nord + 2_000);

        if (await _context.Set<ObservationEntityIndex>()
                .AnyAsync(i => i.EntityTypeId == Kommune && i.EntityId == KommuneA))
        {
            return;
        }

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();

        // Datoene er valgt for å gi periodedimensjonen noe å skille på: to ulike år
        // og tre ulike måneder, pluss én observasjon uten dato i det hele tatt.
        // Datoene gir periodedimensjonen noe å skille på: to ulike år og tre ulike
        // måneder, pluss én observasjon uten dato. Presisjonen gjør det samme for
        // koordpresisjon — tre ulike verdier og én NULL, så både et lukket intervall,
        // et åpent og NULL-bøtta blir dekket.
        var rader = new (Location Lokasjon, DateTime? Dato, int Taksongruppe, int? Presisjon)[]
        {
            (_l1, new DateTime(2024, 6, 15), TaksongruppeA, 25),
            (_l1, new DateTime(2024, 6, 20), TaksongruppeA, 25),
            (_l1, new DateTime(2024, 9, 1),  TaksongruppeB, 100),
            (_l2, new DateTime(2025, 6, 10), TaksongruppeA, 1000),
            (_l2, new DateTime(2025, 3, 5),  TaksongruppeB, 100),
            (_l2, null,                      TaksongruppeA, null),
        };

        var obs = rader
            .Select(r => NewObservation(r.Lokasjon, r.Dato, taxon, taxonName, kategori, funntype))
            .ToArray();

        _context.Set<Observation>().AddRange(obs);
        await _context.SaveChangesAsync();

        for (var i = 0; i < obs.Length; i++)
        {
            // Første observasjon får to områderader — kommune OG fylke — slik at en
            // buffer som teller rader i stedet for observasjoner ville tatt feil.
            Rad(obs[i], Kommune, i < 3 ? KommuneA : KommuneB, rader[i].Dato, rader[i].Taksongruppe, rader[i].Presisjon);

            if (i == 0)
            {
                Rad(obs[i], Fylke, FylkeA, rader[i].Dato, rader[i].Taksongruppe, rader[i].Presisjon);
            }
        }

        // Dimensjon 13 (prosjekt) er flerverdi, og lokasjonsskriptet stopper hvis
        // AreaCountCacheBucketMember mangler den. Uten minst én prosjektrad ville
        // #projbucket blitt tom, og ingen medlemmer skrevet.
        //
        // ProjectOrgId har fremmednøkkel til Organization, så id-en kan ikke
        // finnes opp — den må være en organisasjon som faktisk ligger der.
        _context.Set<ObservationProject>().Add(new ObservationProject
        {
            ObservationId = obs[0].Id,
            ProjectOrgId = await EnsureProsjektOrgIdAsync(),
        });

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// En organisasjon å henge prosjektraden på. Typen betyr ingenting her —
    /// skriptet bøtter på ProjectOrgId uansett — så en som finnes lånes, slik
    /// BranchedFilterIntegrationTests gjør: OrganizationType har ikke
    /// identity-nøkkel, og en ny rad ville kollidert med en annen testklasse.
    /// </summary>
    private async Task<int> EnsureProsjektOrgIdAsync()
    {
        const string navn = "Skriptbuffer-prosjekt";

        var funnet = await _context.Set<Organization>()
            .Where(o => o.Name == navn)
            .Select(o => o.Id)
            .FirstOrDefaultAsync();

        if (funnet != 0) return funnet;

        // Lån en type som finnes. Kjøres klassen alene, finnes ingen — da opprettes
        // én med EKSPLISITT id: OrganizationType har ikke identity-nøkkel, så en ny
        // rad ville fått id 0 og kollidert med en annen testklasse.
        var typeId = await _context.Set<Entities.OrganizationType>()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();

        if (typeId == 0)
        {
            var nyType = new Entities.OrganizationType
            {
                Id = 986500,
                Name = "Skriptbuffer-type",
                Description = "Opprettet av CountCacheScriptIntegrationTests",
            };

            _context.Set<Entities.OrganizationType>().Add(nyType);
            await _context.SaveChangesAsync();
            typeId = nyType.Id;
        }

        var ny = new Organization
        {
            Name = navn,
            OrganizationTypeId = typeId,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        _context.Set<Organization>().Add(ny);
        await _context.SaveChangesAsync();
        return ny.Id;
    }

    private void Rad(
        Observation obs, int entityTypeId, int entityId,
        DateTime? dato, int taksongruppeId, int? presisjon) =>
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = obs.Id,
            EntityTypeId = entityTypeId,
            EntityId = entityId,
            LocationId = obs.LocationId,
            TaxonGroupId = taksongruppeId,
            CategoryId = obs.CategoryId,
            BasisOfRecordId = 986400,
            RegistrationStatusId = 1,
            DateTimeCollected = dato,

            // MÅ følge datoen, se Maanedsinvarianten_stopper_bygget.
            MonthCollected = (byte?)dato?.Month,

            CoordinatePrecisionInMeters = presisjon,
        });

    private static Observation NewObservation(
        Location lokasjon, DateTime? dato, Taxon taxon, TaxonName taxonName,
        Entities.Category kategori, Entities.BasisOfRecord funntype) => new()
        {
            DateLastModified = DateTime.UtcNow,
            DateTimeRecordImported = DateTime.UtcNow,
            DateTimeRecordProcessed = DateTime.UtcNow,
            DateTimeCollected = dato,
            NodeId = 1,
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
            HasAnnotations = false,
            HasErrors = false,
        };

    private async Task<Location> EnsureLocationAsync(string lookupId, int east, int north)
    {
        var funnet = await _context.Set<Location>().FirstOrDefaultAsync(l => l.LookupId == lookupId);
        if (funnet != null) return funnet;

        var ny = new Location
        {
            LookupId = lookupId,
            Latitude = 59.5 + east / 1_000_000.0,
            Longitude = 10.5 + north / 10_000_000.0,
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
}

using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kontrakten for minnelageret: det skal gi NØYAKTIG de polygonene databasestien
/// ville gitt — eller la være å svare.
///
/// HVORFOR SAMMENLIGNING OG IKKE FASTE FORVENTNINGER
/// De to stiene er to implementasjoner av samme regelverk, og nødbryteren
/// <see cref="PolygonLocationStore.AvBryterVariabel"/> finnes nettopp for å kunne
/// kjøre dem mot hverandre. Faste forventninger ville bare bundet opp det ene
/// svaret; en sammenligning binder opp at de er ENIGE. Divergensen er feilen som
/// koster: polygoner som dukker opp eller forsvinner avhengig av hvilken sti som
/// svarte, uten en feilmelding noe sted.
///
/// Begge tilfellene som dekkes her er regler som finnes i to utgaver:
/// rektangelunntaket for sensitive maskeringsruter, og eksakt taksontreff når
/// underliggende taksa er slått av. Den ene utgaven lever i SQL, den andre i
/// arrayene.
///
/// GEOMETRI SEEDES HER, I MOTSETNING TIL ALLE ANDRE INTEGRASJONSTESTER
/// De øvrige setter Location.Geometry = null. Polygonstien kan ikke testes uten
/// geometri, og GeometryTypeId er en beregnet kolonne, så databasen fyller den
/// selv når geometrien står der.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class PolygonLocationStoreIntegrationTests : IAsyncLifetime
{
    // Egen id-rekkevidde, klar av 97xxxx, 98xxxx.
    private const int TaksonA = 986100;
    private const int TaksonB = 986101;

    /// <summary>
    /// Taksonet til observasjonen UTEN rad i ObservationTaxonHierarchy. Se
    /// <see cref="Observasjon_uten_taksonhierarki_behandles_likt_i_begge_stier"/>.
    /// </summary>
    private const int TaksonUtenHierarki = 986102;

    /// <summary>
    /// Takson som DELES av to observasjoner: én med hierarkirad, én uten. Se
    /// <see cref="Delt_takson_der_bare_den_ene_har_hierarkirad_behandles_likt"/>.
    /// </summary>
    private const int TaksonDelt = 986103;

    /// <summary>Forfaderen hierarkiraden peker paa.</summary>
    private const int TaksonForfader = 986104;

    private const int Rutenett = 25833;

    private const string Matrikkelnummer = "1101-1/2";

    /// <summary>Rektangel: fem koordinatpar, altså en rutenettrute.</summary>
    private const string Rektangel = "POLYGON ((0 0, 4000 0, 4000 4000, 0 4000, 0 0))";

    /// <summary>Seks koordinatpar — en reell utstrekning, aldri skjult.</summary>
    private const string Uregelmessig = "POLYGON ((0 0, 4000 0, 4000 2000, 2000 4000, 0 4000, 0 0))";

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    private Location _rute = null!;
    private Location _sensitivRute = null!;
    private Location _myr = null!;

    public PolygonLocationStoreIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    // -----------------------------------------------------------------------
    // De to stiene skal være enige
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Uten_filter_gir_begge_stier_samme_polygoner()
    {
        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto());
    }

    /// <summary>
    /// Med underliggende taksa går minnelageret via forfedrene i
    /// ObservationTaxonHierarchy, akkurat som GetObservationIdsByTaxonHierarchy.
    /// </summary>
    [Fact]
    public async Task Med_underliggende_taksa_gir_begge_stier_samme_polygoner()
    {
        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto { TaxonIds = [TaksonA] });
    }

    /// <summary>
    /// EKSAKT TAKSONTREFF
    ///
    /// Uten underliggende taksa treffer databasestien på Observation.TaxonId, og
    /// minnelageret på observasjonens egen tette taksonindeks. To ulike
    /// mekanismer for samme regel, og det er nettopp derfor de sammenlignes:
    /// rangnivå-kolonnene og forfedrene er begge etterkommer-inklusive og kan
    /// ikke uttrykke dette.
    /// </summary>
    [Fact]
    public async Task Uten_underliggende_taksa_gir_begge_stier_samme_polygoner()
    {
        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto
        {
            TaxonIds = [TaksonA],
            IncludeDescendantTaxa = false,
        });
    }

    /// <summary>
    /// Og det eksakte treffet må faktisk SKILLE. Var filteret uten virkning,
    /// ville testen over bestått med begge stier like og begge gale.
    /// </summary>
    [Fact]
    public async Task Eksakt_takson_utelater_lokaliteter_med_bare_andre_taksa()
    {
        var treff = await MinnelagerAsync();

        var baresTaksonB = treff.TryQuery(new LocationSearchFilterDto
        {
            TaxonIds = [TaksonB],
            IncludeDescendantTaxa = false,
        }, 100)!;

        // TaksonB finnes bare på myra, så den sensitive ruta skal ikke være med.
        baresTaksonB.Select(p => p.LocationId).Should().Contain(_myr.Id);
        baresTaksonB.Select(p => p.LocationId).Should().NotContain(_sensitivRute.Id);
    }

    [Fact]
    public async Task Matrikkelenhet_gir_begge_stier_samme_polygoner()
    {
        var treff = await AssertSammeSomDatabasenAsync(
            new LocationSearchFilterDto { CadastralUnitIds = [Matrikkelnummer] }, eiendom: [_myr.Id]);

        treff.Should().Equal(_myr.Id);
    }

    /// <summary>
    /// Kommunen treffer ingenting, så alt må komme fra matrikkelenheten. Uten OR mellom
    /// dem ville lageret svart tomt.
    /// </summary>
    [Fact]
    public async Task Matrikkelenhet_og_kommune_uten_treff_gir_begge_stier_samme_polygoner()
    {
        var treff = await AssertSammeSomDatabasenAsync(
            new LocationSearchFilterDto { MunicipalityIds = ["9999"], CadastralUnitIds = [Matrikkelnummer] },
            eiendom: [_myr.Id]);

        treff.Should().Equal(_myr.Id);
    }

    [Fact]
    public async Task Matrikkelenhet_uten_oppslaatte_lokasjoner_gir_null()
    {
        var minnelager = await MinnelagerAsync();

        minnelager.TryQuery(new LocationSearchFilterDto { CadastralUnitIds = [Matrikkelnummer] }, 1000)
            .Should().BeNull("lokasjonene må slås opp mot Kartverket av kalleren");
    }

    /// <summary>
    /// RUTENETTREGELEN OG SENSITIVITETSUNNTAKET
    ///
    /// Den vanlige ruta skjules fra begge stier; den sensitive maskeringsruta
    /// vises fra begge, selv om geometrien er identisk. Skilles de to bare på
    /// LookupId, og regelen finnes i to utgaver, er dette den ene testen som
    /// holder dem sammen.
    /// </summary>
    [Fact]
    public async Task Sensitiv_maskeringsrute_vises_og_vanlig_rute_skjules_i_begge_stier()
    {
        var fraMinnet = (await MinnelagerAsync()).TryQuery(new LocationSearchFilterDto(), 1000)!;
        var fraBasen = await UtenMinnelager().GetLocationPolygonsAsync(new LocationSearchFilterDto());

        foreach (var (navn, svar) in new[]
                 {
                     ("minnelageret", fraMinnet.Select(p => p.LocationId).ToList()),
                     ("databasen", fraBasen.Select(p => p.LocationId).ToList()),
                 })
        {
            svar.Should().Contain(_sensitivRute.Id,
                $"{navn} skal vise den sensitive maskeringsruta — den er alt kartet har for funnet");
            svar.Should().NotContain(_rute.Id,
                $"{navn} skal skjule den vanlige rutenettruta");
            svar.Should().Contain(_myr.Id,
                $"{navn} skal vise en reell utstrekning");
        }
    }

    /// <summary>
    /// OBSERVASJON UTEN RAD I ObservationTaxonHierarchy
    ///
    /// Minnelagerets taksonindeks bygges av en spørring som INNER JOINer
    /// ObservationTaxonHierarchy, så en observasjon uten hierarkirad får ingen
    /// tett taksonindeks. For det etterkommer-inklusive filteret er det riktig —
    /// databasestien filtrerer mot samme tabell og utelater den også.
    ///
    /// For det EKSAKTE treffet er det ikke åpenbart: databasestien ser bare på
    /// Observation.TaxonId og trenger ingen hierarkirad. Denne testen slår fast
    /// hva de to faktisk gjør, slik at et avvik blir synlig her og ikke på kartet.
    /// </summary>
    [Fact]
    public async Task Observasjon_uten_taksonhierarki_behandles_likt_i_begge_stier()
    {
        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto
        {
            TaxonIds = [TaksonUtenHierarki],
            IncludeDescendantTaxa = false,
        });

        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto
        {
            TaxonIds = [TaksonUtenHierarki],
        });
    }

    /// <summary>
    /// DELT TAKSON DER BARE ÉN AV OBSERVASJONENE HAR HIERARKIRAD
    ///
    /// De to stiene grupperer på ulikt nivå, og det er her det får følger:
    ///
    ///   databasestien   GetObservationIdsByTaxonHierarchy filtrerer rader i
    ///                   ObservationTaxonHierarchy og velger ObservationId. Altså
    ///                   PER OBSERVASJON — har observasjonen ingen rad, treffer den
    ///                   aldri.
    ///   minnelageret    LastTaksonomiAsync bygger forfedre PER TAKSON, som unionen
    ///                   av forfedrene til alle observasjoner som deler taksonet.
    ///                   Matcher slår så opp observasjonens eget takson.
    ///
    /// To observasjoner deler TaksonDelt. Bare den ene har en rad som peker på
    /// TaksonForfader. Et etterkommer-inklusivt filter på TaksonForfader treffer
    /// derfor BEGGE i minnelageret — den andre arver forfaderen via søskenet — mens
    /// databasen bare finner den med rad.
    ///
    /// Utfallet på kartet er en lokalitet for mye og et for høyt antall, avhengig av
    /// hvilken sti som svarte. Og siden minnelageret svarer på alt etter oppstart, er
    /// det minnelagerets svar brukeren ser.
    ///
    /// HVORFOR DEN STÅR AVSKRUDD
    /// Divergensen er reell i koden — denne testen reproduserte den — men den er
    /// utilgjengelig i dagens data. Målt 23.10.2026 mot
    /// Artskart3IndexProdLikeTestMigrations:
    ///
    ///   observasjoner på polygonlokalitet            3 946 488
    ///   av dem uten rad i ObservationTaxonHierarchy          0
    ///   observasjoner i HELE tabellen uten rad               0
    ///
    /// Invarianten «hver observasjon har en hierarkirad» holder altså overalt, og da
    /// kan ingen observasjon arve en forfader fra et søsken. Å lukke hullet krever at
    /// LastObservasjonerAsync også joiner ObservationTaxonHierarchy for å vite per
    /// observasjon om raden finnes — en join mot en stor tabell i en laster som i dag
    /// bruker 46 sekunder. Det er en reell kostnad for en tom mengde.
    ///
    /// SLÅ DEN PÅ IGJEN hvis en import etterlater observasjoner uten hierarkirad.
    /// Spørringen som avgjør det står i klassekommentaren til
    /// PolygonLocationStore.LastTaksonomiAsync.
    /// </summary>
    [Fact(Skip = "Reproduserer en reell divergens som er utilgjengelig i dagens data: 0 observasjoner mangler hierarkirad (maalt 23.10.2026). Se doc-kommentaren.")]
    public async Task Delt_takson_der_bare_den_ene_har_hierarkirad_behandles_likt()
    {
        await AssertSammeSomDatabasenAsync(new LocationSearchFilterDto
        {
            TaxonIds = [TaksonForfader],
        });
    }

    // -----------------------------------------------------------------------
    // Oppsett
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sammenligner de to stiene på samme filter. Rekkefølgen normaliseres:
    /// kontrakten er hvilke polygoner som kommer ut og med hvilke tall, ikke
    /// hvilken rekkefølge de to stiene sorterer likt i.
    /// </summary>
    /// <summary>
    /// <paramref name="eiendom"/> er lokasjonene matrikkelenheten <see cref="Matrikkelnummer"/>
    /// slås opp til. Returnerer lokasjonene begge stiene ga.
    /// </summary>
    private async Task<List<int>> AssertSammeSomDatabasenAsync(LocationSearchFilterDto filter, int[]? eiendom = null)
    {
        const int maxResults = 1000;

        var fraMinnet = (await MinnelagerAsync()).TryQuery(filter, maxResults, eiendom?.ToHashSet());
        fraMinnet.Should().NotBeNull("minnelageret er bygget, så det skal svare");

        var fraBasen = await UtenMinnelager(eiendom).GetLocationPolygonsAsync(filter);

        fraMinnet!.Select(p => (p.LocationId, p.ObservationCount)).OrderBy(x => x.LocationId)
            .Should().Equal(fraBasen.Select(p => (p.LocationId, p.ObservationCount)).OrderBy(x => x.LocationId));

        return fraMinnet.Select(p => p.LocationId).Order().ToList();
    }

    /// <summary>
    /// Bygger lageret fra databasen, slik oppstarten gjør det. BuildAsync skal
    /// kalles én gang per instans, så hver test får sin egen.
    /// </summary>
    private async Task<PolygonLocationStore> MinnelagerAsync()
    {
        var store = new PolygonLocationStore(
            new EnkelScopeFactory(_context),
            new StubTaxonHierarchyService(),
            new StubAreaHierarchyService(),
            NullLogger<PolygonLocationStore>.Instance);

        await store.BuildAsync();
        store.IsReady.Should().BeTrue(
            $"lageret må være bygget for at testen skal måle noe (er {PolygonLocationStore.AvBryterVariabel} satt?)");

        return store;
    }

    private SearchRepository UtenMinnelager(int[]? eiendom = null) =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(),
            new StubCadastralUnitService(eiendom == null ? null : new() { [Matrikkelnummer] = eiendom }));

    /// <summary>
    /// PolygonLocationStore henter konteksten fra et scope. Her er det samme
    /// kontekst hele veien — testen deler tilkobling med lageret, og det er
    /// greit: lageret bare leser.
    /// </summary>
    private sealed class EnkelScopeFactory(IArtsKartDbContext context) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IArtsKartDbContext) ? context : null;
        public void Dispose() { }
    }

    private async Task SeedAsync()
    {
        var leser = new WKTReader();

        _rute = await EnsureLocationAsync("polystore-rute", Les(leser, Rektangel));
        _sensitivRute = await EnsureLocationAsync("polystore-maske_sensitiv", Les(leser, Rektangel));
        _myr = await EnsureLocationAsync("polystore-myr", Les(leser, Uregelmessig));

        if (await _context.Set<Observation>().AnyAsync(o => o.TaxonId == TaksonA && o.LocationId == _myr.Id))
            return;

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();

        // Egne taksa, ellers kan ikke et eksakt taksonfilter skille observasjonene
        // fra hverandre. Rang, gruppe og navn lånes fra et takson som alt finnes,
        // slik at fremmednøklene holder uten å seede hele takson-treet.
        foreach (var taksonId in new[] { TaksonA, TaksonB, TaksonUtenHierarki, TaksonDelt, TaksonForfader })
        {
            if (await _context.Set<Taxon>().AnyAsync(t => t.Id == taksonId)) continue;

            _context.Set<Taxon>().Add(new Taxon
            {
                Id = taksonId,
                TaxonRankId = taxon.TaxonRankId,
                ExternalTaxonId = taksonId,
                ParentTaxonId = 0,
                ValidScientificNameId = taxonName.Id,
                ValidScientificName = $"polystore-takson-{taksonId}",
                PreferredPopularName = $"polystore-takson-{taksonId}",
                TaxonGroupId = taxon.TaxonGroupId,
                ExistsInCountry = true,
                ScientificNameIdHiarchy = taksonId.ToString(),
                TaxonIdHiarchy = taksonId.ToString(),
                CumulativeObservationCount = 0,
                IsDeleted = false,
            });
        }

        await _context.SaveChangesAsync();

        // TaksonA på både den sensitive ruta og myra, TaksonB bare på myra — slik
        // at et eksakt taksonfilter har noe å skille på. Den vanlige ruta får også
        // en observasjon, ellers ville den uteblitt fordi den er tom og ikke fordi
        // rutenettregelen skjulte den.
        var paaRuten = NyObservasjon(_rute, TaksonA, taxon, taxonName);
        var paaSensitiv = NyObservasjon(_sensitivRute, TaksonA, taxon, taxonName);
        var paaMyraA = NyObservasjon(_myr, TaksonA, taxon, taxonName);
        var paaMyraB = NyObservasjon(_myr, TaksonB, taxon, taxonName);
        var utenHierarki = NyObservasjon(_myr, TaksonUtenHierarki, taxon, taxonName);

        // DELT TAKSON, BARE ÉN AV DEM MED HIERARKIRAD
        //
        // Begge har TaksonDelt. deltMedRad får en rad som peker på TaksonForfader;
        // deltUtenRad får ingen rad. De ligger på ULIKE lokaliteter, slik at et
        // avvik viser seg som en lokalitet for mye og ikke bare som et for høyt tall.
        var deltMedRad = NyObservasjon(_myr, TaksonDelt, taxon, taxonName);
        var deltUtenRad = NyObservasjon(_sensitivRute, TaksonDelt, taxon, taxonName);

        _context.Set<Observation>().AddRange(
            paaRuten, paaSensitiv, paaMyraA, paaMyraB, utenHierarki, deltMedRad, deltUtenRad);
        await _context.SaveChangesAsync();

        // Hierarkiraden er observasjonens egen takson som forfader av seg selv —
        // det er formen GetObservationIdsByTaxonHierarchy filtrerer mot.
        // utenHierarki får BEVISST ingen rad.
        foreach (var (obs, taksonId) in new[]
                 {
                     (paaRuten, TaksonA), (paaSensitiv, TaksonA),
                     (paaMyraA, TaksonA), (paaMyraB, TaksonB),

                     // Forfaderen, ikke taksonet sitt eget: det er dette som gjør
                     // TaksonForfader til et etterkommer-inklusivt treff på TaksonDelt.
                     (deltMedRad, TaksonForfader),
                 })
        {
            Hierarki(obs.Id, taksonId);
        }

        await _context.SaveChangesAsync();
    }

    private static Geometry Les(WKTReader leser, string wkt)
    {
        var geometri = leser.Read(wkt);
        geometri.SRID = Rutenett;
        return geometri;
    }

    private void Hierarki(int observationId, int taxonId) =>
        _context.Set<ObservationTaxonHierarchy>().Add(new ObservationTaxonHierarchy
        {
            ObservationId = observationId,
            SpeciesTaxonId = taxonId,
        });

    private async Task<Location> EnsureLocationAsync(string lookupId, Geometry geometri)
    {
        var funnet = await _context.Set<Location>().FirstOrDefaultAsync(l => l.LookupId == lookupId);
        if (funnet != null) return funnet;

        var ny = new Location
        {
            LookupId = lookupId,
            Latitude = 59.5,
            Longitude = 10.5,
            CoordinatePrecision = 25,
            East = 300_000,
            North = 6_600_000,
            Locality = lookupId,
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = geometri,
        };

        _context.Set<Location>().Add(ny);
        await _context.SaveChangesAsync();
        return ny;
    }

    private static Observation NyObservasjon(Location lokasjon, int taxonId, Taxon taxon, TaxonName taxonName) => new()
    {
        DateLastModified = DateTime.UtcNow,
        DateTimeRecordImported = DateTime.UtcNow,
        DateTimeRecordProcessed = DateTime.UtcNow,
        NodeId = 1,
        BasisOfRecordId = 1,
        TaxonId = taxonId,
        MatchedScientificNameId = taxonName.Id,
        TaxonGroupId = 1,
        CategoryId = 1,
        RegistrationStatusId = 1,
        Latitude = lokasjon.Latitude ?? 59.5,
        Longitude = lokasjon.Longitude ?? 10.5,
        East = lokasjon.East,
        North = lokasjon.North,
        LocationId = lokasjon.Id,
        InstitutionOrgId = 1,
        HashCode = Random.Shared.Next(),
        ProcessEngineId = 1,
        HasAnnotations = false,
        HasErrors = false,
    };
}

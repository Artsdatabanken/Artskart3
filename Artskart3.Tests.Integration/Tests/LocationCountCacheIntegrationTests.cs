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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kontrakten for lokasjonsbufferen: et oppslag skal gi nøyaktig de lokasjonene
/// tellingen ville gitt, med nøyaktig de samme tallene — eller la være å svare.
///
/// Testene sammenligner derfor to repositories mot hverandre, ett med buffer og
/// ett uten, på samme filter. En buffer som svarer «nesten riktig» er verre enn
/// ingen buffer: feilen ville vist seg som prikker på kartet, uten noen
/// feilmelding.
///
/// FLERE INDEKSRADER PER OBSERVASJON ER HELE POENGET
/// Områdebufferen kan telle rader, fordi den grupperer PÅ området. Denne
/// grupperer på lokasjon, og da havner alle områderadene til én observasjon i
/// samme gruppe. Seedet gir derfor observasjoner med tre og fire områderader:
/// en buffer bygget med COUNT(*) ville svart 3 der tellingen svarer 1, og
/// sammenligningen fanger det.
///
/// Bufferen bygges i testen fra HELE indekstabellen, slik byggejobben skal gjøre
/// det. Bøttet den bare sine egne rader, ville de andre lokasjonene manglet i
/// begge svarene, og sammenligningen bestått uten å bevise noe.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class LocationCountCacheIntegrationTests : IAsyncLifetime
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;
    private const int Havomraade = (int)ObservationIndexEntityType.OceanArea;
    private const int Verneomraade = (int)ObservationIndexEntityType.RestrictedArea;
    private const int Institusjon = (int)ObservationIndexEntityType.Institution;

    // Egen id-rekkevidde, klar av 97xxxx, 98[1234]xxx.
    private const int KommuneA = 985010;
    private const int KommuneB = 985011;
    private const int FylkeA = 985020;
    private const int HavA = 985030;
    private const int VerneA = 985040;

    private const string KommuneAFid = "985010";
    private const string FylkeAFid = "985020";

    private const int TaksongruppeA = 985200;
    private const int TaksongruppeB = 985201;
    private const int KategoriA = 985300;
    private const int KategoriB = 985301;

    // Tre lokasjoner: to nær hverandre og én langt unna, slik at kartutsnittet
    // faktisk har noe å skille på.
    private const int NaerOst = 300_000;
    private const int NaerNord = 6_600_000;
    private const int FjernOst = 900_000;
    private const int FjernNord = 7_900_000;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;
    private Location _l1 = null!;
    private Location _l2 = null!;
    private Location _l3 = null!;

    public LocationCountCacheIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await ClearCacheAsync();
        await _context.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Bufferen svarer, og svarer likt
    // -----------------------------------------------------------------------

    /// <summary>
    /// Det ufiltrerte kallet. Det er det tregeste vi har målt — rundt to sekunder
    /// for Oslo-utsnittet — og det eneste som ikke har noen dimensjon i
    /// AreaCountCacheDimensions. Derfor har lokasjonsbufferen sin egen
    /// dimensjon 0.
    /// </summary>
    [Fact]
    public async Task Ufiltrert_gir_samme_lokasjoner_som_telling()
    {
        await BuildAsync(LocationCountCacheService.NoFilterDimensionId, _ => LocationCountCacheService.NoFilterBucketId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto());
    }

    [Fact]
    public async Task Ett_filter_gir_samme_lokasjoner_som_telling()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] });
    }

    /// <summary>
    /// To verdier i samme dimensjon. Hver observasjon har nøyaktig én
    /// taksongruppe, så summen over bøttene teller hver observasjon én gang — og
    /// oppslaget må gruppere, ikke bare lese én rad per lokasjon.
    /// </summary>
    [Fact]
    public async Task Flere_verdier_i_samme_dimensjon_summeres()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaksongruppeA, TaksongruppeB],
        });
    }

    [Fact]
    public async Task Kategorifilter_gir_samme_lokasjoner_som_telling()
    {
        await BuildAsync(KategoriDimensjon, r => r.CategoryId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { CategoryIds = [KategoriB] });
    }

    /// <summary>
    /// Kartutsnittet er det som snevrer mest inn, og det er derfor East og North
    /// ligger i bufferen i det hele tatt. Utsnittet her dekker de to nære
    /// lokasjonene og ikke den fjerne.
    /// </summary>
    [Fact]
    public async Task Kartutsnittet_filtrerer_paa_koordinater()
    {
        await BuildAsync(LocationCountCacheService.NoFilterDimensionId, _ => LocationCountCacheService.NoFilterBucketId);

        var resultat = await WithCache().GetLocationsAsync(new LocationSearchFilterDto { Envelope = NaertUtsnitt() });

        resultat.Should().Contain(x => x.Id == _l1.Id);
        resultat.Should().Contain(x => x.Id == _l2.Id);
        resultat.Should().NotContain(x => x.Id == _l3.Id);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { Envelope = NaertUtsnitt() });
    }

    [Fact]
    public async Task Kartutsnitt_og_filter_samtidig_gir_samme_som_telling()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaksongruppeA],
            Envelope = NaertUtsnitt(),
        });
    }

    /// <summary>
    /// KJERNEN. Observasjon 0 har fire områderader på samme lokasjon. En buffer
    /// bygget med COUNT(*) ville talt den fire ganger; tellingen teller distinkte
    /// observasjoner. Uten denne testen ville forskjellen bare vist seg som for
    /// høye tall på kartet.
    /// </summary>
    [Fact]
    public async Task Observasjon_med_flere_omraaderader_telles_en_gang()
    {
        await BuildAsync(LocationCountCacheService.NoFilterDimensionId, _ => LocationCountCacheService.NoFilterBucketId);

        var medBuffer = await WithCache().GetLocationsAsync(new LocationSearchFilterDto());
        var utenBuffer = await WithoutCache().GetLocationsAsync(new LocationSearchFilterDto());

        var buffret = medBuffer.Single(x => x.Id == _l1.Id);
        var talt = utenBuffer.Single(x => x.Id == _l1.Id);

        buffret.ObservationCount.Should().Be(talt.ObservationCount);

        // Tre observasjoner på L1, den ene med fire områderader. Står det 7 her,
        // teller bufferen rader.
        buffret.ObservationCount.Should().Be(3);
    }

    /// <summary>
    /// Institusjonsradene (type 101) er ikke områder, og tellestien utelater dem.
    /// Fordi begge stier teller DISTINKTE observasjoner, viser feilen seg bare
    /// når en observasjon har institusjonsrad OG ingen områderad: den skal ikke
    /// telle med i det hele tatt.
    ///
    /// L3 har to observasjoner — nummer 6 med kommunerad og nummer 7 med bare
    /// institusjonsrad. Riktig svar er 1. En buffer som ikke filtrerte på type
    /// 101 ville svart 2.
    /// </summary>
    [Fact]
    public async Task Observasjon_med_bare_institusjonsrad_teller_ikke_med()
    {
        await BuildAsync(LocationCountCacheService.NoFilterDimensionId, _ => LocationCountCacheService.NoFilterBucketId);

        await AssertSameAsCountingAsync(new LocationSearchFilterDto());

        var medBuffer = await WithCache().GetLocationsAsync(new LocationSearchFilterDto());

        medBuffer.Single(x => x.Id == _l3.Id).ObservationCount.Should().Be(1,
            "observasjon 7 har bare en institusjonsrad, og den er ikke et omraade");
    }

    /// <summary>
    /// Tiebreakeren. Take kutter på antall, og ved like tall må begge stier velge
    /// de samme lokasjonene. For Oslo-utsnittet ligger grensen ved TOP 100 000 på
    /// to observasjoner, og 42 308 lokasjoner har nøyaktig to — så dette er ikke
    /// en teoretisk bekymring.
    /// </summary>
    [Fact]
    public async Task Take_kutter_likt_i_begge_stier_ved_like_antall()
    {
        await BuildAsync(LocationCountCacheService.NoFilterDimensionId, _ => LocationCountCacheService.NoFilterBucketId);

        var filter = new LocationSearchFilterDto { Envelope = NaertUtsnitt(), MaxResults = 1 };

        var medBuffer = await WithCache().GetLocationsAsync(filter);
        var utenBuffer = await WithoutCache().GetLocationsAsync(filter);

        medBuffer.Should().HaveCount(1);
        medBuffer.Select(x => x.Id).Should().Equal(utenBuffer.Select(x => x.Id));
    }

    /// <summary>
    /// Et filter som ikke treffer noen bøtte skal gi tomt, aldri ufiltrert. En
    /// buffer som falt tilbake til «alle bøtter» ville bestått alle testene over.
    /// </summary>
    [Fact]
    public async Task Filter_uten_treff_gir_tomt_ikke_ufiltrert()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        var resultat = await WithCache().GetLocationsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [999999] });

        resultat.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Geografi — flerverdi, og den farligste å ta feil av
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Omraadefilter_gir_samme_lokasjoner_som_telling()
    {
        await BuildGeografiAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { MunicipalityIds = [KommuneAFid] });
    }

    /// <summary>
    /// Fylkes-IDer slås opp mot både fylke og Svalbard, som ellers i løsningen.
    /// Bøttene må pakke områdetypen inn i medlems-id-en, ellers kollapser kommune
    /// 985020 og fylke 985020 til samme medlem.
    /// </summary>
    [Fact]
    public async Task Fylkesfilter_gir_samme_lokasjoner_som_telling()
    {
        await BuildGeografiAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { CountyIds = [FylkeAFid] });
    }

    /// <summary>
    /// DEN VIKTIGE. Er medlemstabellen ikke bygget, treffer ingen medlemmer noen
    /// bøtte. Ble det lest som «ingen lokasjoner», ville et fylkesvalg gitt et
    /// tomt kart uten noen feilmelding. Riktig svar er å ikke svare.
    /// </summary>
    [Fact]
    public async Task Manglende_medlemstabell_gir_null_ikke_tomt()
    {
        await BuildGeografiAsync();
        await _context.Set<AreaCountCacheBucketMember>().ExecuteDeleteAsync();

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(
            new LocationSearchFilterDto { MunicipalityIds = [KommuneAFid] }, 100);

        resultat.Should().BeNull("uten medlemstabell kan bufferen ikke vite hvilke boetter som gjelder");
    }

    /// <summary>
    /// Er medlemstabellen bygget, men området ikke finnes i noen bøtte, ligger
    /// det ingen observasjoner der. Da er tomt riktig — og forskjellen fra testen
    /// over er hele grunnen til at ResolveAsync har tre utfall og ikke to.
    /// </summary>
    [Fact]
    public async Task Ukjent_omraade_med_bygget_medlemstabell_gir_tomt()
    {
        await BuildGeografiAsync();

        var resultat = await WithCache().GetLocationsAsync(
            new LocationSearchFilterDto { MunicipalityIds = ["985999"] });

        resultat.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Når bufferen skal la være å svare
    // -----------------------------------------------------------------------

    [Fact]
    public async Task To_filtre_gir_null()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(new LocationSearchFilterDto
        {
            TaxonGroupIds = [TaksongruppeA],
            CategoryIds = [KategoriA],
        }, 100);

        resultat.Should().BeNull("bufferen lagrer marginaler, ikke samfordelingen");
    }

    [Fact]
    public async Task Katalognummer_gir_null()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(
            new LocationSearchFilterDto { ObservationIds = [1, 2, 3] }, 100);

        resultat.Should().BeNull();
    }

    [Fact]
    public async Task Ubygget_buffer_gir_null()
    {
        await ClearCacheAsync();

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(new LocationSearchFilterDto(), 100);

        resultat.Should().BeNull();
    }

    [Fact]
    public async Task Feil_skjemaversjon_gir_null()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);
        await SetSchemaVersionAsync(AreaCountCacheDimensions.SchemaVersion + 1);

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] }, 100);

        resultat.Should().BeNull();
    }

    [Fact]
    public async Task Status_som_ikke_er_Ready_gir_null()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);
        await SetStatusAsync("Building");

        var resultat = await CreateCacheService().TryGetTopLocationsAsync(
            new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] }, 100);

        resultat.Should().BeNull();
    }

    [Fact]
    public async Task Avslaatt_buffer_gir_null()
    {
        await BuildAsync(TaksongruppeDimensjon, r => r.TaxonGroupId);

        var resultat = await CreateCacheService(new LocationCountCacheOptions { Enabled = false })
            .TryGetTopLocationsAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] }, 100);

        resultat.Should().BeNull();
    }

    /// <summary>
    /// Uten buffer skal lokasjonene være uendret. Det er hele grunnen til at
    /// migrasjonen kan deployes før byggejobben finnes.
    /// </summary>
    [Fact]
    public async Task Repository_faller_tilbake_til_telling_naar_bufferen_ikke_svarer()
    {
        await ClearCacheAsync();

        await AssertSameAsCountingAsync(new LocationSearchFilterDto { TaxonGroupIds = [TaksongruppeA] });
    }

    // -----------------------------------------------------------------------
    // Oppsett
    // -----------------------------------------------------------------------

    private static byte TaksongruppeDimensjon =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Taksongruppe").Id;

    private static byte KategoriDimensjon =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Kategori").Id;

    private static byte GeografiDimensjon =>
        AreaCountCacheDimensions.All.Single(d => d.Name == "Geografi").Id;

    private static EnvelopeDto NaertUtsnitt() => new()
    {
        MinX = NaerOst - 10_000,
        MaxX = NaerOst + 10_000,
        MinY = NaerNord - 10_000,
        MaxY = NaerNord + 10_000,
    };

    private SearchRepository WithCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService(), null, CreateCacheService());

    private SearchRepository WithoutCache() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new StubAreaHierarchyService(), new StubTaxonHierarchyService());

    private LocationCountCacheService CreateCacheService(LocationCountCacheOptions? options = null) =>
        new(_context, new StubTaxonHierarchyService(), new StubAreaHierarchyService(),
            Options.Create(options ?? new LocationCountCacheOptions()),
            NullLogger<LocationCountCacheService>.Instance);

    /// <summary>
    /// Kjører samme filter med og uten buffer og krever identisk resultat, lokasjon
    /// for lokasjon. Sammenligner på (id, antall), ikke bare på antall lokasjoner:
    /// to svar kan ha like mange lokasjoner og likevel fordele observasjonene ulikt.
    /// </summary>
    private async Task AssertSameAsCountingAsync(LocationSearchFilterDto filter)
    {
        var medBuffer = await WithCache().GetLocationsAsync(filter);
        var utenBuffer = await WithoutCache().GetLocationsAsync(filter);

        medBuffer.Select(x => (x.Id, x.ObservationCount)).OrderBy(x => x.Id)
            .Should().Equal(utenBuffer.Select(x => (x.Id, x.ObservationCount)).OrderBy(x => x.Id));
    }

    private async Task ClearCacheAsync()
    {
        await _context.Set<LocationCountCacheLevel1>().ExecuteDeleteAsync();
        await _context.Set<LocationCountCacheState>().ExecuteDeleteAsync();
        await _context.Set<AreaCountCacheBucketMember>().ExecuteDeleteAsync();
    }

    private async Task MarkReadyAsync()
    {
        _context.Set<LocationCountCacheState>().Add(new LocationCountCacheState
        {
            Id = 1,
            SchemaVersion = AreaCountCacheDimensions.SchemaVersion,
            Status = "Ready",
            BuiltAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private Task SetStatusAsync(string status) =>
        _context.Set<LocationCountCacheState>().Where(s => s.Id == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));

    private Task SetSchemaVersionAsync(int version) =>
        _context.Set<LocationCountCacheState>().Where(s => s.Id == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SchemaVersion, version));

    /// <summary>
    /// Bygger bufferen fra HELE indekstabellen, slik byggejobben skal gjøre det.
    ///
    /// Speiler BuildLocationCountCache.sql: samme kildepredikat, og DISTINKTE
    /// observasjoner per (bøtte, lokasjon) — ikke rader. Grupperer man på lokasjon,
    /// havner alle områderadene til én observasjon i samme gruppe, og COUNT(*)
    /// ville talt områdemedlemskap.
    /// </summary>
    private async Task BuildAsync(byte dimensionId, Func<ObservationEntityIndex, int?> bucketOf)
    {
        await ClearCacheAsync();

        var rader = await KilderaderAsync();
        var koordinater = await KoordinaterAsync();

        _context.Set<LocationCountCacheLevel1>().AddRange(rader
            .Select(r => (
                Bucket: bucketOf(r) ?? AreaCountCacheDimensions.NullBucket,
                LocationId: r.LocationId!.Value,
                r.ObservationId))
            .Where(x => x.Bucket != AreaCountCacheDimensions.NullBucket)
            .GroupBy(x => (x.Bucket, x.LocationId))
            .Select(g => new LocationCountCacheLevel1
            {
                DimensionId = dimensionId,
                BucketId = g.Key.Bucket,
                LocationId = g.Key.LocationId,
                East = koordinater[g.Key.LocationId].East,
                North = koordinater[g.Key.LocationId].North,
                ObservationCount = g.Select(x => x.ObservationId).Distinct().Count(),
            }));

        await MarkReadyAsync();
    }

    /// <summary>
    /// Bygger geografi-dimensjonen og medlemstabellen den slås opp i. Bøtta er
    /// hele mengden områder en observasjon tilhører, på tvers av områdetyper, og
    /// medlems-id-en pakker typen inn — kommune 985020 og fylke 985020 er ulike
    /// områder.
    /// </summary>
    private async Task BuildGeografiAsync()
    {
        await ClearCacheAsync();

        var alle = await _context.Set<ObservationEntityIndex>().AsNoTracking().ToListAsync();
        var koordinater = await KoordinaterAsync();

        int[] geografiTyper =
        [
            AreaCountCacheDimensions.AreaTypeMunicipality,
            AreaCountCacheDimensions.AreaTypeCounty,
            AreaCountCacheDimensions.AreaTypeRestricted,
            AreaCountCacheDimensions.AreaTypeOcean,
            AreaCountCacheDimensions.AreaTypeSvalbard,
        ];

        var medlemskap = alle
            .Where(r => geografiTyper.Contains(r.EntityTypeId))
            .GroupBy(r => r.ObservationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => AreaCountCacheDimensions.PackAreaMember(r.EntityTypeId, r.EntityId))
                      .Distinct().OrderBy(m => m).ToArray());

        var boetteFor = medlemskap.Values
            .Select(m => string.Join(',', m))
            .Distinct()
            .Select((noekkel, i) => (noekkel, boette: i + 1))
            .ToDictionary(x => x.noekkel, x => x.boette);

        _context.Set<AreaCountCacheBucketMember>().AddRange(boetteFor
            .SelectMany(kv => kv.Key.Split(',').Select(medlem => new AreaCountCacheBucketMember
            {
                DimensionId = GeografiDimensjon,
                MemberId = int.Parse(medlem),
                BucketId = kv.Value,
            })));

        var boetteForObservasjon = medlemskap.ToDictionary(
            kv => kv.Key, kv => boetteFor[string.Join(',', kv.Value)]);

        var rader = await KilderaderAsync();

        _context.Set<LocationCountCacheLevel1>().AddRange(rader
            .Select(r => (
                Bucket: boetteForObservasjon.TryGetValue(r.ObservationId, out var b)
                    ? b
                    : AreaCountCacheDimensions.NullBucket,
                LocationId: r.LocationId!.Value,
                r.ObservationId))
            .Where(x => x.Bucket != AreaCountCacheDimensions.NullBucket)
            .GroupBy(x => (x.Bucket, x.LocationId))
            .Select(g => new LocationCountCacheLevel1
            {
                DimensionId = GeografiDimensjon,
                BucketId = g.Key.Bucket,
                LocationId = g.Key.LocationId,
                East = koordinater[g.Key.LocationId].East,
                North = koordinater[g.Key.LocationId].North,
                ObservationCount = g.Select(x => x.ObservationId).Distinct().Count(),
            }));

        await MarkReadyAsync();
    }

    /// <summary>
    /// Kildepredikatet, ordrett som i GetLocationsAsync. Endres det ene, må det
    /// andre endres i takt — ellers svarer de to stiene ulikt.
    /// </summary>
    private Task<List<ObservationEntityIndex>> KilderaderAsync() =>
        _context.Set<ObservationEntityIndex>().AsNoTracking()
            .Where(idx => idx.EntityTypeId != Institusjon && idx.LocationId != null)
            .ToListAsync();

    private async Task<Dictionary<int, (int East, int North)>> KoordinaterAsync() =>
        (await _context.Set<Location>().AsNoTracking()
            .Select(l => new { l.Id, l.East, l.North })
            .ToListAsync())
        .ToDictionary(l => l.Id, l => (l.East, l.North));

    // -----------------------------------------------------------------------

    private async Task SeedAsync()
    {
        _l1 = await EnsureLocationAsync("loccache-1", NaerOst, NaerNord);
        _l2 = await EnsureLocationAsync("loccache-2", NaerOst + 1_000, NaerNord + 1_000);
        _l3 = await EnsureLocationAsync("loccache-3", FjernOst, FjernNord);

        if (await _context.Set<ObservationEntityIndex>()
                .AnyAsync(i => i.EntityTypeId == Kommune && i.EntityId == KommuneA))
        {
            return;
        }

        var taxon = await _context.Set<Taxon>().OrderBy(t => t.Id).FirstAsync();
        var taxonName = await _context.Set<TaxonName>().OrderBy(t => t.Id).FirstAsync();
        var kategori = await _context.Set<Entities.Category>().OrderBy(c => c.Id).FirstAsync();
        var funntype = await _context.Set<Entities.BasisOfRecord>().OrderBy(b => b.Id).FirstAsync();

        // Åtte observasjoner: 0-2 på L1, 3-5 på L2, 6-7 på L3.
        var paaLokasjon = new[] { _l1, _l1, _l1, _l2, _l2, _l2, _l3, _l3 };

        var obs = paaLokasjon
            .Select(l => NewObservation(l, taxon, taxonName, kategori, funntype))
            .ToArray();

        _context.Set<Observation>().AddRange(obs);
        await _context.SaveChangesAsync();

        // Observasjon 0 har FIRE områderader på samme lokasjon. Det er tilfellet
        // en buffer bygget med COUNT(*) ville talt fire ganger.
        Rad(obs[0], Kommune, KommuneA, TaksongruppeA, KategoriA);
        Rad(obs[0], Fylke, FylkeA, TaksongruppeA, KategoriA);
        Rad(obs[0], Havomraade, HavA, TaksongruppeA, KategoriA);
        Rad(obs[0], Verneomraade, VerneA, TaksongruppeA, KategoriA);

        Rad(obs[1], Kommune, KommuneA, TaksongruppeB, KategoriA);
        Rad(obs[2], Kommune, KommuneA, TaksongruppeA, KategoriB);

        // Observasjon 3 har tre områderader, denne gangen på L2.
        Rad(obs[3], Kommune, KommuneB, TaksongruppeA, KategoriA);
        Rad(obs[3], Fylke, FylkeA, TaksongruppeA, KategoriA);
        Rad(obs[3], Havomraade, HavA, TaksongruppeA, KategoriA);

        Rad(obs[4], Kommune, KommuneB, TaksongruppeB, KategoriB);
        Rad(obs[5], Kommune, KommuneB, TaksongruppeB, KategoriB);

        // Observasjon 6 ligger på den fjerne lokasjonen — der for at
        // kartutsnittet skal ha noe å utelate.
        Rad(obs[6], Kommune, KommuneB, TaksongruppeA, KategoriA);

        // Observasjon 7 har BARE en institusjonsrad. Den er ikke et område, og
        // begge stier skal se bort fra den.
        Rad(obs[7], Institusjon, 985500, TaksongruppeA, KategoriA);

        await _context.SaveChangesAsync();
    }

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

    private void Rad(Observation obs, int entityTypeId, int entityId, int taxonGroupId, int categoryId) =>
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = obs.Id,
            EntityTypeId = entityTypeId,
            EntityId = entityId,
            LocationId = obs.LocationId,
            TaxonGroupId = taxonGroupId,
            CategoryId = categoryId,
            BasisOfRecordId = 985400,
            RegistrationStatusId = 1,
        });

    private static Observation NewObservation(
        Location lokasjon, Taxon taxon, TaxonName taxonName,
        Entities.Category kategori, Entities.BasisOfRecord funntype) => new()
    {
        DateLastModified = DateTime.UtcNow,
        DateTimeRecordImported = DateTime.UtcNow,
        DateTimeRecordProcessed = DateTime.UtcNow,
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
        HasErrors = false,
        HasAnnotations = false,
    };
}

using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
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
/// Områdetellingen hopper over geo-semijoinen når den ikke kan fjerne noen rad.
/// Se <see cref="AreaCountGeoJoinRules"/> for de to reglene og hvorfor den ene er
/// et bevis og den andre en forutsetning.
///
/// Enhetstestene vokter regelen. Disse vokter at REGELEN ER KOBLET RIKTIG INN —
/// at spørringen faktisk gir samme tall, og at semijoinen fortsatt står der den
/// skal. Uten den siste delen ville en for vid regel gitt for høye tall på
/// kartet uten at noe feilet.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class AreaCountGeoJoinIntegrationTests : IAsyncLifetime
{
    private const int Kommune = (int)ObservationIndexEntityType.Municipality;
    private const int Fylke = (int)ObservationIndexEntityType.County;

    // Egen id-rekkevidde, klar av 97xxxx, 98[1234]xxx og 984xxx.
    private const string FylkeFid = "985050";
    private const int FylkeId = 985050;
    private const string KommuneAFid = "985051";
    private const int KommuneAId = 985051;
    private const string KommuneBFid = "985052";
    private const int KommuneBId = 985052;

    private const int ObsBase = 985100;
    private const int TaksongruppeA = 985200;
    private const int TaksongruppeB = 985201;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    public AreaCountGeoJoinIntegrationTests(DatabaseFixture db) => _db = db;

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

    private SearchRepository Repo() =>
        new(_context, NullLogger<SearchRepository>.Instance, Options.Create(new PaginationOptions()),
            new HierarkiStub(), new StubTaxonHierarchyService());

    private async Task<int?> AntallFor(int zoom, LocationSearchFilterDto filter, string fid)
    {
        var counts = await Repo().GetAreaCountsAsync(zoom, filter);
        return counts.FirstOrDefault(c => c.Fid == fid)?.ObservationCount;
    }

    // --- DELMENGDEREGELEN ----------------------------------------------------

    /// <summary>
    /// Zoomnivå 1 med fylkesfilter — tilfellet semijoinen var overflødig i.
    /// Fylket har fire observasjoner, tre av dem i taksongruppe A.
    /// </summary>
    [Fact]
    public async Task Z1_fylkesfilter_teller_riktig()
    {
        var antall = await AntallFor(1,
            new LocationSearchFilterDto { CountyIds = [FylkeFid], TaxonGroupIds = [TaksongruppeA] },
            FylkeFid);

        antall.Should().Be(3);
    }

    /// <summary>
    /// Samme filter uten attributtfilter går en annen vei (ingen dynamisk
    /// telling). Tatt med fordi en endring i tellestien ikke skal kunne påvirke
    /// den stien uten at det synes.
    /// </summary>
    [Fact]
    public async Task Z1_fylkesfilter_med_begge_taksongrupper_teller_alle()
    {
        var antall = await AntallFor(1,
            new LocationSearchFilterDto
            {
                CountyIds = [FylkeFid],
                TaxonGroupIds = [TaksongruppeA, TaksongruppeB],
            },
            FylkeFid);

        antall.Should().Be(4);
    }

    // --- ETTERKOMMERREGELEN --------------------------------------------------

    /// <summary>
    /// Zoomnivå 2 med fylkesfilter: utdata er kommunene i fylket, og det er
    /// fylkesraden som tilfredsstiller predikatet. Her hviler svaret på
    /// invarianten som BackfillAll.sql håndhever.
    /// </summary>
    [Fact]
    public async Task Z2_fylkesfilter_fordeler_paa_kommunene()
    {
        var filter = new LocationSearchFilterDto
        {
            CountyIds = [FylkeFid],
            TaxonGroupIds = [TaksongruppeA],
        };

        (await AntallFor(2, filter, KommuneAFid)).Should().Be(2);
        (await AntallFor(2, filter, KommuneBFid)).Should().Be(2);
    }

    // --- OPPRULLING FRA KOMMUNE TIL FYLKE ------------------------------------

    /// <summary>
    /// DEN VIKTIGSTE. Zoomnivå 1 med KOMMUNEfilter: utdatacellen er fylket, men
    /// bare én kommune er valgt. Fylket har tre observasjoner i taksongruppe A,
    /// kommune A har to av dem.
    ///
    /// Vises fylkets fulle antall (3) i stedet for kommunens (2), er svaret for
    /// høyt — og uten noen feilmelding. Det er feilen både en for vid
    /// <see cref="AreaCountGeoJoinRules"/> og en opprulling som ignorerer
    /// kommunevalget ville gitt.
    /// </summary>
    [Fact]
    public async Task Z1_kommunefilter_viser_bare_kommunens_andel()
    {
        var antall = await AntallFor(1,
            new LocationSearchFilterDto
            {
                MunicipalityIds = [KommuneAFid],
                TaxonGroupIds = [TaksongruppeA],
            },
            FylkeFid);

        antall.Should().Be(2, "bare kommune A er valgt, ikke hele fylket");
    }

    /// <summary>
    /// DEN ANDRE VIKTIGE. To kommuner i samme fylke, og én observasjon ligger i
    /// begge.
    ///
    /// Kommune A har obs 0 og 3, kommune B har obs 2 og 3 — til sammen tre
    /// distinkte observasjoner. Summeres kommunetallene i stedet for å telles
    /// distinkt, står det 4. Det er nøyaktig feilen
    /// <see cref="AreaCountRollupRules"/> er skrevet for å unngå, og den som
    /// rammer 0,38 % av observasjonene i produksjon.
    /// </summary>
    [Fact]
    public async Task Z1_to_kommuner_teller_grenseobservasjon_en_gang()
    {
        var antall = await AntallFor(1,
            new LocationSearchFilterDto
            {
                MunicipalityIds = [KommuneAFid, KommuneBFid],
                TaxonGroupIds = [TaksongruppeA],
            },
            FylkeFid);

        antall.Should().Be(3, "observasjonen på kommunegrensa skal telles én gang, ikke to");
    }

    /// <summary>
    /// Velges både fylke og kommune, er geo-predikatet en ELLER: observasjoner i
    /// fylket skal med selv om de ligger utenfor kommunen. Opprullingen fra
    /// kommunerader kan ikke uttrykke det, så den skal la være å svare og
    /// overlate tellingen til den vanlige stien.
    /// </summary>
    [Fact]
    public async Task Z1_fylke_og_kommune_sammen_faller_tilbake_til_eller()
    {
        var antall = await AntallFor(1,
            new LocationSearchFilterDto
            {
                CountyIds = [FylkeFid],
                MunicipalityIds = [KommuneAFid],
                TaxonGroupIds = [TaksongruppeA],
            },
            FylkeFid);

        antall.Should().Be(3, "fylkesvalget slipper gjennom obs 2, som ligger utenfor kommune A");
    }

    // -------------------------------------------------------------------------

    private async Task SeedAsync()
    {
        if (await _context.Set<Area>().AnyAsync(a => a.Fid == FylkeFid)) return;

        _context.Set<Area>().AddRange(
            // Area.ParentFid er NOT NULL. Fylket har ingen forelder, så det får en
            // plassholder som med vilje ikke lar seg tolke som en id — da ville en
            // regel som feilaktig leste forelderen til et fylke feilet her.
            NyttOmraade(FylkeFid, "Geojoin fylke", Fylke, zoomLevel: 1, parentFid: "geojoin-rot"),
            NyttOmraade(KommuneAFid, "Geojoin kommune A", Kommune, zoomLevel: 2, parentFid: FylkeFid),
            NyttOmraade(KommuneBFid, "Geojoin kommune B", Kommune, zoomLevel: 2, parentFid: FylkeFid));

        // Tre observasjoner: to i kommune A (én per taksongruppe), én i kommune B.
        // ALLE har både kommuneraden og fylkesraden — det er invarianten
        // etterkommerregelen hviler på, og uten den ville Z2-testen målt noe annet.
        Rad(ObsBase + 0, Kommune, KommuneAId, TaksongruppeA);
        Rad(ObsBase + 0, Fylke, FylkeId, TaksongruppeA);

        Rad(ObsBase + 1, Kommune, KommuneAId, TaksongruppeB);
        Rad(ObsBase + 1, Fylke, FylkeId, TaksongruppeB);

        Rad(ObsBase + 2, Kommune, KommuneBId, TaksongruppeA);
        Rad(ObsBase + 2, Fylke, FylkeId, TaksongruppeA);

        // Observasjon 3 ligger i BEGGE kommunene — lokasjonen er på
        // kommunegrensa. 213 302 observasjoner (0,38 %) er slik i produksjon,
        // og det er dem opprullingen ville dobbelttalt om den summerte
        // kommunetallene i stedet for å telle distinkt.
        Rad(ObsBase + 3, Kommune, KommuneAId, TaksongruppeA);
        Rad(ObsBase + 3, Kommune, KommuneBId, TaksongruppeA);
        Rad(ObsBase + 3, Fylke, FylkeId, TaksongruppeA);

        await _context.SaveChangesAsync();
    }

    private void Rad(int observationId, int entityTypeId, int entityId, int taxonGroupId) =>
        _context.Set<ObservationEntityIndex>().Add(new ObservationEntityIndex
        {
            ObservationId = observationId,
            EntityTypeId = entityTypeId,
            EntityId = entityId,
            TaxonGroupId = taxonGroupId,
            CategoryId = 985300,
            BasisOfRecordId = 985400,
            RegistrationStatusId = 1,
        });

    private static Area NyttOmraade(string fid, string navn, int areaTypeId, int zoomLevel, string parentFid)
    {
        var omraade = new Area
        {
            ParentFid = parentFid,
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = fid,
            Name = navn,
            AreaTypeId = areaTypeId,
            ZoomLevel = zoomLevel,
            SyncDateTime = DateTime.UtcNow,
            ObservationCount = 0,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
        };
        return omraade;
    }

    /// <summary>
    /// Egen stub fordi <c>StubAreaHierarchyService.GetCountyFid</c> utleder
    /// fylket av de to første tegnene i kommune-Fid-en. Det binder testen til et
    /// bestemt Fid-format, og formatet kolliderer med ekte områder i seed-dataene.
    /// Her er forelderkoblingen eksplisitt i stedet.
    /// </summary>
    private sealed class HierarkiStub : IAreaHierarchyService
    {
        private static readonly Dictionary<string, string> KommuneTilFylke = new()
        {
            [KommuneAFid] = FylkeFid,
            [KommuneBFid] = FylkeFid,
        };

        public string? GetCountyFid(string municipalityFid) =>
            KommuneTilFylke.GetValueOrDefault(municipalityFid);

        public IReadOnlyList<string> GetMunicipalityFids(string countyFid) =>
            countyFid == FylkeFid ? [KommuneAFid, KommuneBFid] : [];

        public int? FidToEntityId(string fid) =>
            int.TryParse(fid.Replace("_", ""), out var id) ? id : null;

        public int? RestrictedAreaFidToEntityId(string fid) =>
            int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : null;

        public int[] FidsToEntityIds(string[]? fids) =>
            fids?.Select(FidToEntityId).Where(i => i.HasValue).Select(i => i!.Value).ToArray() ?? [];

        public int[] RestrictedAreaFidsToEntityIds(string[]? fids) =>
            fids?.Select(RestrictedAreaFidToEntityId).Where(i => i.HasValue).Select(i => i!.Value).ToArray() ?? [];

        public AreaBounds? GetAreaBounds(int entityTypeId, int entityId) => null;

        public int[] PruneToEnvelope(int[] entityIds, AreaBounds envelope, params int[] entityTypeIds) => entityIds;

        public int[] FilterToExistingAreas(int[] entityIds, int entityTypeId) => entityIds;
    }
}

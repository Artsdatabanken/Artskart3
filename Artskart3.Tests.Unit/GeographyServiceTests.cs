using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.IO;
using Geometry = NetTopologySuite.Geometries.Geometry;

namespace Artskart3.Tests.Unit;

public class GeographyServiceTests
{
    private static readonly WKTReader Leser = new(NtsGeometryServices.Instance);

    /// <summary>
    /// Områdekilde med håndlagde polygoner. Oppslagslogikken er den samme enten den får
    /// tre områder eller 4 224.
    /// </summary>
    private sealed class FakeKilde(params AreaGeometry[] områder) : IAreaGeometrySource
    {
        public int Lastinger { get; private set; }

        public IReadOnlyList<AreaGeometry> LoadAll()
        {
            Lastinger++;
            return områder;
        }
    }

    private static Geometry Rute(double minX, double minY, double maxX, double maxY) =>
        Leser.Read($"POLYGON (({minX} {minY}, {maxX} {minY}, {maxX} {maxY}, {minX} {maxY}, {minX} {minY}))");

    private static AreaGeometry Område(
        int id,
        string fid,
        string navn,
        int areaTypeId,
        bool isRequired,
        Geometry geometri,
        bool isCurrent = true,
        string? parentFid = null) =>
        new(id, fid, navn, areaTypeId, isRequired, isCurrent, parentFid, geometri);

    private static GeographyService Tjeneste(IAreaGeometrySource kilde) =>
        new(kilde, new ProjNetCoordinateTransformer(), NullLogger<GeographyService>.Instance);

    // --- Områdeoppslag ------------------------------------------------------

    /// <summary>En kommune (påkrevd type 1) og et verneområde (ikke påkrevd) som overlapper.</summary>
    private static FakeKilde OsloKilde() => new(
        Område(1, "0301", "Oslo", 1, true, Rute(250000, 6630000, 275000, 6660000), parentFid: "03"),
        Område(2, "03", "Oslo", 2, true, Rute(240000, 6620000, 285000, 6670000)),
        Område(3, "Naturbase VV123", "Nordmarka", 3, false, Rute(255000, 6645000, 265000, 6655000)));

    [Fact]
    public void Finner_omradene_en_koordinat_ligger_i()
    {
        var tjeneste = Tjeneste(OsloKilde());

        var treff = tjeneste.GetAreasForCoordinate(260000, 6650000);

        treff.Select(x => x.Fid).Should().Equal(["0301", "03", "Naturbase VV123"]);
    }

    /// <summary>Rekkefølgen er områdetype, så Fid — den samme Artskart 2 sorterer på.</summary>
    [Fact]
    public void Sorterer_paa_omradetype_og_deretter_fid()
    {
        var kilde = new FakeKilde(
            Område(1, "0302", "B", 1, true, Rute(0, 0, 100, 100)),
            Område(2, "0301", "A", 1, true, Rute(0, 0, 100, 100)),
            Område(3, "03", "Fylke", 2, true, Rute(0, 0, 100, 100)));

        var treff = Tjeneste(kilde).GetAreasForCoordinate(50, 50);

        treff.Select(x => x.Fid).Should().Equal(["0301", "0302", "03"]);
    }

    [Fact]
    public void Gir_ingen_omrader_utenfor_alle_polygoner()
    {
        Tjeneste(OsloKilde()).GetAreasForCoordinate(900000, 7900000).Should().BeEmpty();
    }

    /// <summary>
    /// Treff bare på en ikke-påkrevd områdetype regnes som utenfor Norge, og da
    /// returneres ingenting — heller ikke verneområdet koordinaten ligger i. Det er
    /// denne regelen som avviser utenlandske koordinater.
    /// </summary>
    [Fact]
    public void Gir_ingen_omrader_naar_bare_ikke_paakrevd_type_treffer()
    {
        var kilde = new FakeKilde(
            Område(3, "Naturbase VV123", "Et verneområde", 3, false, Rute(0, 0, 100, 100)));

        Tjeneste(kilde).GetAreasForCoordinate(50, 50).Should().BeEmpty();
    }

    [Fact]
    public void Tar_med_historiske_omrader()
    {
        var kilde = new FakeKilde(
            Område(1, "0301", "Oslo", 1, true, Rute(0, 0, 100, 100)),
            Område(2, "0101_2019", "Halden gammel", 1, true, Rute(0, 0, 100, 100), isCurrent: false));

        var treff = Tjeneste(kilde).GetAreasForCoordinate(50, 50);

        treff.Should().HaveCount(2, "en observasjon fra 1950 hører til kommunen som fantes da");
    }

    [Fact]
    public void Laster_omradene_bare_en_gang()
    {
        var kilde = OsloKilde();
        var tjeneste = Tjeneste(kilde);

        tjeneste.GetAreasForCoordinate(260000, 6650000);
        tjeneste.GetAreasForCoordinate(260000, 6650000);
        tjeneste.TryGetMunicipalityCentroid("Oslo", "Oslo", out _);

        kilde.Lastinger.Should().Be(1);
    }

    // --- Nær Norge ----------------------------------------------------------

    [Theory]
    [InlineData(262000, 6643000)]   // Oslo
    [InlineData(653423, 7731687)]   // Tromsø
    [InlineData(510000, 8650000)]   // Svalbard
    public void CloseToNorway_godtar_norske_koordinater(int east, int north)
    {
        Tjeneste(new FakeKilde()).CloseToNorway(east, north).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0)]              // nullkoordinat
    [InlineData(500000, 5400000)]   // Mellom-Europa
    [InlineData(-900000, 7000000)]  // vest i Atlanteren
    public void CloseToNorway_avviser_koordinater_utenfor(int east, int north)
    {
        Tjeneste(new FakeKilde()).CloseToNorway(east, north).Should().BeFalse();
    }

    // --- footprintWKT -------------------------------------------------------

    [Fact]
    public void Tolker_omriss_i_utm()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        var ok = tjeneste.TryValidateFootprint(
            "POLYGON ((260000 6640000, 262000 6640000, 262000 6642000, 260000 6642000, 260000 6640000))",
            footprintSrs: null,
            decimals: 2,
            out var resultat);

        ok.Should().BeTrue();
        resultat!.Geometry.SRID.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        resultat.CentroidLatLon.Should().BeNull("omrisset var alt i meter");

        // Halve diagonalen i et 2000 x 2000 m rektangel.
        resultat.PrecisionInMeters.Should().Be((int)(0.5 * Math.Sqrt(2000.0 * 2000 + 2000 * 2000)));
    }

    /// <summary>
    /// Oppgir kilden EPSG:4326, transformeres omrisset til UTM 33 — og senterpunktet i
    /// grader tas vare på, fordi kallstedet bruker det som observasjonens koordinat når
    /// posten ellers mangler én.
    /// </summary>
    [Fact]
    public void Transformerer_omriss_oppgitt_som_epsg4326()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        var ok = tjeneste.TryValidateFootprint(
            "POLYGON ((10.70 59.90, 10.80 59.90, 10.80 59.95, 10.70 59.95, 10.70 59.90))",
            footprintSrs: "EPSG:4326",
            decimals: 2,
            out var resultat);

        ok.Should().BeTrue();
        resultat!.Geometry.Coordinates.Should().OnlyContain(c => c.X > 200000 && c.X < 350000);
        resultat.CentroidLatLon.Should().NotBeNull();
        resultat.CentroidLatLon!.Value.X.Should().BeApproximately(10.75, 0.01);
        resultat.CentroidLatLon.Value.Y.Should().BeApproximately(59.925, 0.01);
    }

    /// <summary>
    /// Uten oppgitt SRS kjennes grader igjen på at nordverdien er under 1000. En
    /// UTM-northing i Norge er over seks millioner.
    /// </summary>
    [Fact]
    public void Kjenner_igjen_grader_paa_at_nordverdien_er_liten()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        var ok = tjeneste.TryValidateFootprint(
            "POLYGON ((10.70 59.90, 10.80 59.90, 10.80 59.95, 10.70 59.95, 10.70 59.90))",
            footprintSrs: null,
            decimals: 2,
            out var resultat);

        ok.Should().BeTrue();
        resultat!.Geometry.Coordinates.Should().OnlyContain(c => c.Y > 6600000);
    }

    [Fact]
    public void Runder_av_til_oppgitt_antall_desimaler()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        tjeneste.TryValidateFootprint(
            "POLYGON ((260000.123456 6640000.123456, 262000 6640000, 262000 6642000, 260000.123456 6640000.123456))",
            null, decimals: 1, out var resultat);

        resultat!.Geometry.Coordinates[0].X.Should().Be(260000.1);
    }

    [Fact]
    public void Lar_koordinatene_sta_naar_desimaler_er_minus_en()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        tjeneste.TryValidateFootprint(
            "POLYGON ((260000.123456 6640000.5, 262000 6640000, 262000 6642000, 260000.123456 6640000.5))",
            null, decimals: -1, out var resultat);

        resultat!.Geometry.Coordinates[0].X.Should().Be(260000.123456);
    }

    /// <summary>
    /// Noen kilder leverer et polygon der siste punkt ikke er lik det første. Artskart 2
    /// limer førstepunktet inn før avslutningsparentesen og prøver på nytt.
    /// </summary>
    [Fact]
    public void Lukker_en_apen_ring_og_proever_paa_nytt()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        var ok = tjeneste.TryValidateFootprint(
            "POLYGON ((260000 6640000, 262000 6640000, 262000 6642000))",
            null, decimals: 2, out var resultat);

        ok.Should().BeTrue();
        resultat!.Geometry.Coordinates.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ikke wkt i det hele tatt")]
    [InlineData("POLYGON ((0 0))")]
    public void Avviser_ugyldig_omriss(string? wkt)
    {
        Tjeneste(new FakeKilde())
            .TryValidateFootprint(wkt, null, 2, out var resultat)
            .Should().BeFalse();

        resultat.Should().BeNull();
    }

    [Fact]
    public void Tolker_ogsaa_punkt_og_linje()
    {
        var tjeneste = Tjeneste(new FakeKilde());

        tjeneste.TryValidateFootprint("POINT (260000 6640000)", null, 2, out var punkt).Should().BeTrue();
        punkt!.PrecisionInMeters.Should().Be(0, "et punkt har ingen utstrekning");

        tjeneste.TryValidateFootprint("LINESTRING (260000 6640000, 263000 6644000)", null, 2, out var linje)
            .Should().BeTrue();
        linje!.PrecisionInMeters.Should().Be(2500, "halve diagonalen i 3000 x 4000 m");
    }

    // --- Kommunesentroider --------------------------------------------------

    private static FakeKilde SentroideKilde() => new(
        Område(1, "0301", "Oslo", 1, true, Rute(250000, 6630000, 270000, 6650000), parentFid: "03"),
        Område(2, "03", "Oslo", 2, true, Rute(240000, 6620000, 280000, 6660000)),
        Område(3, "4601", "Bergen", 1, true, Rute(-30000, 6700000, -10000, 6720000), parentFid: "46"),
        Område(4, "46", "Vestland", 2, true, Rute(-60000, 6650000, 20000, 6800000)));

    [Fact]
    public void Finner_senterpunkt_paa_kommune_og_fylke()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Oslo", "Oslo", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("kommune+fylke");
        treff.Point.X.Should().Be(260000);
        treff.Point.Y.Should().Be(6640000);
        treff.Point.Epsg.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        treff.PrecisionInMeters.Should().Be((int)(0.5 * Math.Sqrt(20000.0 * 20000 + 20000 * 20000)));
    }

    [Fact]
    public void Finner_senterpunkt_paa_kommunenavn_alene()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Bergen", "Et fylke som ikke finnes", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("kommunenavn");
    }

    [Fact]
    public void Faller_tilbake_til_fylket()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("En kommune som ikke finnes", "Vestland", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("fylke");
    }

    /// <summary>Kommunenavnet står i fylkesfeltet og omvendt — vanlig nok i kildedata.</summary>
    [Fact]
    public void Taaler_at_kommune_og_fylke_er_byttet_om()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Vestland", "Bergen", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("byttet om");
    }

    [Fact]
    public void Bruker_kommunefeltet_som_fylkesnavn_til_slutt()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Vestland", "Et fylke som ikke finnes", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("kommunefeltet som fylkesnavn");
    }

    /// <summary>
    /// Står fylkesnavnet i kommunefeltet og fylkesfeltet er tomt, treffer «byttet om»
    /// først: nøkkelen den slår opp blir (tomt, kommunefeltet), som er den samme formen
    /// fylker er lagret på. Leddet «kommunefeltet som fylkesnavn» er derfor bare
    /// nåelig når fylkesfeltet er utfylt med noe som ikke finnes.
    /// </summary>
    [Fact]
    public void Byttet_om_treffer_forst_naar_fylkesfeltet_er_tomt()
    {
        var ok = Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Vestland", null, out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("byttet om");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Gir_ingen_sentroide_uten_kommunenavn(string? kommune)
    {
        Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid(kommune, "Oslo", out var treff)
            .Should().BeFalse();

        treff.Should().BeNull();
    }

    [Fact]
    public void Gir_ingen_sentroide_naar_ingenting_treffer()
    {
        Tjeneste(SentroideKilde())
            .TryGetMunicipalityCentroid("Reykjavik", "Island", out _)
            .Should().BeFalse();
    }

    /// <summary>Ampersand skrives «og» i områdenavnene, og byttet gjøres på begge sider.</summary>
    [Fact]
    public void Bytter_ampersand_med_og()
    {
        var kilde = new FakeKilde(
            Område(1, "5401", "Nord-Troms og Finnmark", 1, true, Rute(0, 0, 100, 100), parentFid: "54"),
            Område(2, "54", "Troms og Finnmark", 2, true, Rute(0, 0, 200, 200)));

        var ok = Tjeneste(kilde)
            .TryGetMunicipalityCentroid("Nord-Troms & Finnmark", "Troms & Finnmark", out var treff);

        ok.Should().BeTrue();
        treff!.MatchedOn.Should().Be("kommune+fylke");
    }

    /// <summary>
    /// Kommunenavn gjenbrukes gjennom kommunereformene. Uten denne regelen kunne en
    /// sammenslått kommune fra 1960 overskrevet dagens.
    /// </summary>
    [Fact]
    public void Gjeldende_omrade_vinner_over_historisk()
    {
        var kilde = new FakeKilde(
            Område(1, "0101_1960", "Halden", 1, true, Rute(0, 0, 100, 100), isCurrent: false, parentFid: "01"),
            Område(2, "3101", "Halden", 1, true, Rute(500, 500, 700, 700), isCurrent: true, parentFid: "01"),
            Område(3, "01", "Østfold", 2, true, Rute(0, 0, 1000, 1000)));

        Tjeneste(kilde).TryGetMunicipalityCentroid("Halden", "Østfold", out var treff);

        treff!.Point.X.Should().Be(600, "den gjeldende kommunen skal vinne");
    }

    /// <summary>
    /// En kommune uten gjenkjent fylke får tomt fylkesnavn. Artskart 2 kaller
    /// <c>.Name</c> på oppslaget uten nullsjekk, og kaster for hele byggingen.
    /// </summary>
    [Fact]
    public void Taaler_kommune_uten_gjenkjent_fylke()
    {
        var kilde = new FakeKilde(
            Område(1, "9999", "Foreldreløs", 1, true, Rute(0, 0, 100, 100), parentFid: "finnes-ikke"),
            Område(2, "0301", "Oslo", 1, true, Rute(500, 500, 600, 600), parentFid: "03"),
            Område(3, "03", "Oslo", 2, true, Rute(400, 400, 700, 700)));

        var tjeneste = Tjeneste(kilde);

        tjeneste.TryGetMunicipalityCentroid("Foreldreløs", null, out var treff).Should().BeTrue();
        treff!.MatchedOn.Should().Be("kommune+fylke", "tomt fylkesnavn er nøkkelen den fikk");

        tjeneste.TryGetMunicipalityCentroid("Oslo", "Oslo", out _).Should().BeTrue(
            "de øvrige kommunene skal fortsatt kunne slås opp");
    }
}

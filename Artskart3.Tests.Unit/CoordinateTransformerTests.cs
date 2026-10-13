using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using NetTopologySuite.Geometries;

namespace Artskart3.Tests.Unit;

public class CoordinateTransformerTests
{
    private readonly ProjNetCoordinateTransformer _transformer = new();

    /// <summary>Et punkt i Oslo, i ETRS89 / UTM 33N.</summary>
    private static readonly GeoPoint OsloUtm33 = new(262000, 6643000, CoordinateSystems.Etrs89UtmZone33N);

    [Fact]
    public void Transform_gir_punktet_urort_naar_systemet_er_det_samme()
    {
        _transformer.Transform(OsloUtm33, CoordinateSystems.Etrs89UtmZone33N).Should().Be(OsloUtm33);
    }

    [Fact]
    public void Transform_fra_utm33_til_wgs84_gir_oslo()
    {
        var resultat = _transformer.Transform(OsloUtm33, CoordinateSystems.Wgs84LatLon);

        resultat.Epsg.Should().Be(CoordinateSystems.Wgs84LatLon);
        resultat.X.Should().BeInRange(10.70, 10.80);
        resultat.Y.Should().BeInRange(59.80, 59.90);
    }

    [Fact]
    public void Transform_tur_retur_gir_samme_punkt()
    {
        var wgs84 = _transformer.Transform(OsloUtm33, CoordinateSystems.Wgs84LatLon);
        var tilbake = _transformer.Transform(wgs84, CoordinateSystems.Etrs89UtmZone33N);

        tilbake.X.Should().BeApproximately(OsloUtm33.X, 0.02);
        tilbake.Y.Should().BeApproximately(OsloUtm33.Y, 0.02);
    }

    /// <summary>
    /// Vakten rundt <c>TOWGS84[-87,-98,-121,0,0,0,0]</c> i ED50-definisjonen. Faller
    /// datumskiftet ut av WKT-en, blir ED50 behandlet som WGS 84, og 40 år med
    /// ED50-koordinater flytter seg hundre meter uten at noe feiler.
    /// </summary>
    [Fact]
    public void Transform_fra_ed50_flytter_punktet_over_hundre_meter()
    {
        var ed50 = new GeoPoint(10.75, 59.91, CoordinateSystems.Ed50LatLon);
        var wgs84 = new GeoPoint(10.75, 59.91, CoordinateSystems.Wgs84LatLon);

        var fraEd50 = _transformer.Transform(ed50, CoordinateSystems.Etrs89UtmZone33N);
        var fraWgs84 = _transformer.Transform(wgs84, CoordinateSystems.Etrs89UtmZone33N);

        var avstand = Math.Sqrt(
            Math.Pow(fraEd50.X - fraWgs84.X, 2) + Math.Pow(fraEd50.Y - fraWgs84.Y, 2));

        avstand.Should().BeInRange(50, 500);
    }

    /// <summary>
    /// ETRS89 har <c>TOWGS84[0,0,0,...]</c>, så ETRS89 og WGS 84 behandles som samme
    /// datum. Det er forutsetningen ommerkingen fra 32633 til 25833 bygger på.
    /// </summary>
    [Fact]
    public void Transform_fra_etrs89_og_wgs84_gir_nesten_samme_punkt()
    {
        var etrs89 = new GeoPoint(10.75, 59.91, CoordinateSystems.Etrs89LatLon);
        var wgs84 = new GeoPoint(10.75, 59.91, CoordinateSystems.Wgs84LatLon);

        var fraEtrs89 = _transformer.Transform(etrs89, CoordinateSystems.Etrs89UtmZone33N);
        var fraWgs84 = _transformer.Transform(wgs84, CoordinateSystems.Etrs89UtmZone33N);

        fraEtrs89.X.Should().BeApproximately(fraWgs84.X, 0.01);
        fraEtrs89.Y.Should().BeApproximately(fraWgs84.Y, 0.01);
    }

    /// <summary>
    /// Forskjellen mellom 32633 og 25833 er bare ellipsoiden, og den gir millimetre.
    /// Måles her fordi plan 4.12.5 lener seg på nettopp det når importen skal skrive
    /// 25833 der Artskart 2 skrev 32633.
    /// </summary>
    [Fact]
    public void Transform_til_32633_og_25833_gir_praktisk_talt_samme_meter()
    {
        var wgs84 = new GeoPoint(10.75, 59.91, CoordinateSystems.Wgs84LatLon);

        var wgs84Utm = _transformer.Transform(wgs84, CoordinateSystems.Wgs84UtmZone33N);
        var etrs89Utm = _transformer.Transform(wgs84, CoordinateSystems.Etrs89UtmZone33N);

        wgs84Utm.X.Should().BeApproximately(etrs89Utm.X, 0.01);
        wgs84Utm.Y.Should().BeApproximately(etrs89Utm.Y, 0.01);
    }

    [Fact]
    public void Transform_kaster_for_et_system_registeret_ikke_kjenner()
    {
        var ukjent = new GeoPoint(1, 1, 1234567);

        var act = () => _transformer.Transform(ukjent, CoordinateSystems.Etrs89UtmZone33N);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TransformGeometry_transformerer_alle_punkter_og_merker_resultatet()
    {
        var fabrikk = new GeometryFactory();
        var polygon = fabrikk.CreatePolygon([
            new Coordinate(10.70, 59.90),
            new Coordinate(10.80, 59.90),
            new Coordinate(10.80, 59.95),
            new Coordinate(10.70, 59.95),
            new Coordinate(10.70, 59.90)
        ]);

        var resultat = _transformer.TransformGeometry(
            polygon, CoordinateSystems.Wgs84LatLon, CoordinateSystems.Etrs89UtmZone33N);

        resultat.SRID.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        resultat.Coordinates.Should().HaveSameCount(polygon.Coordinates);
        resultat.Coordinates.Should().OnlyContain(c => c.X > 200000 && c.X < 350000);
        resultat.Coordinates.Should().OnlyContain(c => c.Y > 6600000 && c.Y < 6700000);
        resultat.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TransformGeometry_rorer_ikke_den_opprinnelige_geometrien()
    {
        var fabrikk = new GeometryFactory();
        var punkt = fabrikk.CreatePoint(new Coordinate(10.75, 59.91));

        _transformer.TransformGeometry(
            punkt, CoordinateSystems.Wgs84LatLon, CoordinateSystems.Etrs89UtmZone33N);

        punkt.X.Should().Be(10.75);
        punkt.Y.Should().Be(59.91);
    }

    [Fact]
    public void TransformGeometry_runder_til_oppgitt_antall_desimaler()
    {
        var fabrikk = new GeometryFactory();
        var punkt = fabrikk.CreatePoint(new Coordinate(10.75, 59.91));

        var resultat = _transformer.TransformGeometry(
            punkt, CoordinateSystems.Wgs84LatLon, CoordinateSystems.Etrs89UtmZone33N, decimals: 2);

        resultat.Coordinate.X.Should().Be(Math.Round(resultat.Coordinate.X, 2));
        resultat.Coordinate.Y.Should().Be(Math.Round(resultat.Coordinate.Y, 2));
    }

    [Fact]
    public void TransformGeometry_med_samme_system_merker_men_transformerer_ikke()
    {
        var fabrikk = new GeometryFactory();
        var punkt = fabrikk.CreatePoint(new Coordinate(262000, 6643000));

        var resultat = _transformer.TransformGeometry(
            punkt, CoordinateSystems.Etrs89UtmZone33N, CoordinateSystems.Etrs89UtmZone33N);

        resultat.SRID.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        resultat.Coordinate.X.Should().Be(262000);
        resultat.Coordinate.Y.Should().Be(6643000);
    }

    /// <summary>
    /// Hele MGRS-stien: rutereferanse → UTM → lengde- og breddegrad. Treffer den
    /// Oslo, virker både tolkningen og transformasjonen.
    /// </summary>
    [Fact]
    public void Mgrs_via_transformator_gir_lengde_og_breddegrad_i_oslo()
    {
        var mgrs = Core.Application.Import.Helpers.MgrsHelper.MgrsToUtm("33VTG62004300");

        var wgs84 = _transformer.Transform(mgrs!.Point, CoordinateSystems.Wgs84LatLon);

        wgs84.X.Should().BeInRange(10.70, 10.80);
        wgs84.Y.Should().BeInRange(59.80, 59.90);
    }

    /// <summary>
    /// WKT-malen for ETRS89 / UTM skal gi den samme strengen som står utskrevet i
    /// Artskart 2s register. Forskjellen ville ikke feilet noe sted — den ville bare
    /// flyttet koordinater.
    /// </summary>
    [Fact]
    public void Registeret_bruker_samme_wkt_som_Artskart2()
    {
        const string fraArtskart2 =
            "PROJCS[\"ETRS89 / UTM zone 33N\",GEOGCS[\"ETRS89\",DATUM[\"European_Terrestrial_Reference_System_1989\",SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]],AUTHORITY[\"EPSG\",\"6258\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4258\"]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",15],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AUTHORITY[\"EPSG\",\"25833\"]]";

        CoordinateSystemRegister.Wkt(CoordinateSystems.Etrs89UtmZone33N).Should().Be(fraArtskart2);
    }

    [Theory]
    [InlineData(30, 25830)]
    [InlineData(33, 25833)]
    [InlineData(37, 25837)]
    public void Etrs89UtmFromZone_og_tilbake(int sone, int epsg)
    {
        CoordinateSystems.Etrs89UtmFromZone(sone).Should().Be(epsg);
        CoordinateSystems.ZoneFromEtrs89Utm(epsg).Should().Be((short)sone);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(38)]
    public void Etrs89UtmFromZone_avviser_soner_utenfor_30_til_37(int sone)
    {
        var act = () => CoordinateSystems.Etrs89UtmFromZone(sone);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Registeret_kjenner_alle_systemene_importen_bruker()
    {
        int[] påkrevde =
        [
            CoordinateSystems.Wgs84LatLon,
            CoordinateSystems.Ed50LatLon,
            CoordinateSystems.Etrs89LatLon,
            CoordinateSystems.Wgs84UtmZone33N,
            25830, 25831, 25832, 25833, 25834, 25835, 25836, 25837
        ];

        foreach (var epsg in påkrevde)
        {
            CoordinateSystemRegister.Get(epsg).Should().NotBeNull($"EPSG {epsg} må finnes i registeret");
        }
    }

    [Fact]
    public void GeoPoint_regner_nullposisjon_som_tom()
    {
        new GeoPoint(0, 0, 25833).IsNull.Should().BeTrue();
        new GeoPoint(262000, 6643000, 25833).IsNull.Should().BeFalse();
    }
}

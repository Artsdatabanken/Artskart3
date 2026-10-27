using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.IO;
using Geometry = NetTopologySuite.Geometries.Geometry;

namespace Artskart3.Tests.Unit;

public class ImportGeometryStepTests
{
    private static readonly WKTReader Leser = new(NtsGeometryServices.Instance);

    private readonly ImportedObservation _observasjon = new() { NodeId = 43 };
    private readonly ImportContext _context = new();

    /// <summary>
    /// Områdekilde med Oslo kommune, Oslo fylke og Jan Mayen. Nok til å dekke både
    /// påkrevde områdetyper og Jan Mayen-regelen.
    /// </summary>
    private sealed class FakeOmrader : IAreaGeometrySource
    {
        public IReadOnlyList<AreaGeometry> LoadAll() =>
        [
            new(1, "0301", "Oslo", 1, true, true, "03", Rute(250000, 6630000, 275000, 6660000)),
            new(2, "03", "Oslo", 2, true, true, null, Rute(240000, 6620000, 285000, 6670000)),
            new(3, "Naturbase VV1", "Nordmarka", 3, false, true, null, Rute(255000, 6645000, 265000, 6655000)),
            new(4, "2201", "Jan Mayen", 6, true, true, null, Rute(-420000, 8180000, -380000, 8220000))
        ];

        private static Geometry Rute(double minX, double minY, double maxX, double maxY) =>
            Leser.Read($"POLYGON (({minX} {minY}, {maxX} {minY}, {maxX} {maxY}, {minX} {maxY}, {minX} {minY}))");
    }

    private static ImportGeometryStep NyttSteg(IAreaGeometrySource? områder = null)
    {
        var transformator = new ProjNetCoordinateTransformer();
        var geografi = new GeographyService(
            områder ?? new FakeOmrader(), transformator, NullLogger<GeographyService>.Instance);

        return new ImportGeometryStep(geografi, transformator);
    }

    private ImportRecordResult Kjør(CacheRecord record, ImportGeometryStep? steg = null) =>
        (steg ?? NyttSteg()).ValidateAndImport(record, _observasjon, _context);

    /// <summary>Oslo sentrum, som lengde- og breddegrad.</summary>
    private static CacheRecord OsloPost() => new()
    {
        NodeDatabaseID = 43,
        Longitude = 10.75,
        Latitude = 59.91,
        Country = "Norway"
    };

    [Fact]
    public void Er_et_parallell_steg()
    {
        NyttSteg().StepType.Should().Be(ImportStepType.Parallel);
    }

    // --- Trinn 1: lengde- og breddegrad ------------------------------------

    [Fact]
    public void Bruker_lengde_og_breddegrad_fra_kilden()
    {
        var resultat = Kjør(OsloPost());

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        _observasjon.Longitude.Should().Be(10.75);
        _observasjon.Latitude.Should().Be(59.91);
        _observasjon.East.Should().BeInRange(262000, 263000);
        _observasjon.North.Should().BeInRange(6648000, 6650000);
    }

    [Fact]
    public void Lager_lokalitet_med_omrader_og_oppslagsnokkel()
    {
        Kjør(OsloPost());

        _observasjon.Location.Should().NotBeNull();
        _observasjon.Location!.LookupId.Should().Be($"43_{_observasjon.East}_{_observasjon.North}_");
        _observasjon.Location.Areas.Select(x => x.Fid).Should().Contain(["0301", "03"]);
        _observasjon.Location.Geometry!.SRID.Should().Be(CoordinateSystems.Etrs89UtmZone33N);
        _context.Location.Should().BeSameAs(_observasjon.Location);
    }

    [Theory]
    [InlineData(91, 10)]
    [InlineData(-91, 10)]
    [InlineData(59, 181)]
    [InlineData(59, -181)]
    public void Avviser_koordinat_utenfor_jorda(double lat, double lon)
    {
        var resultat = Kjør(new CacheRecord { Latitude = lat, Longitude = lon });

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.CoordinateOutOfBounds);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.InvalidLongitudeLatitude);
    }

    [Fact]
    public void Runder_lengde_og_breddegrad_til_seks_desimaler()
    {
        Kjør(new CacheRecord { Longitude = 10.7512345678, Latitude = 59.9112345678, Country = "Norway" });

        _observasjon.Longitude.Should().Be(10.751235);
        _observasjon.Latitude.Should().Be(59.911235);
    }

    /// <summary>
    /// Avviker den beregnede koordinaten mer enn ca. én meter fra kildens, lagres
    /// kildens verdi som <c>_original_</c> og postens felt overskrives. Det skjer når
    /// koordinaten er transformert eller utledet — her fra ED50.
    /// </summary>
    [Fact]
    public void Lagrer_kildens_koordinat_naar_den_er_transformert()
    {
        var record = new CacheRecord
        {
            Longitude = 10.75, Latitude = 59.91, GeodeticDatum = "ed50", Country = "Norway"
        };

        Kjør(record);

        _context.DynamicProperties["_original_Longitude"].Should().Be("10.75");
        _context.DynamicProperties["_original_Latitude"].Should().Be("59.91");
        record.Longitude.Should().Be(_observasjon.Longitude, "posten skal bære den tolkede verdien videre");
    }

    /// <summary>
    /// Avrundingen til seks desimaler alene flytter aldri koordinaten mer enn 5e-7
    /// grader, altså under terskelen på 9e-6. Den utløser derfor ingen
    /// <c>_original_</c>-verdi.
    /// </summary>
    [Fact]
    public void Avrunding_alene_gir_ingen_originalverdi()
    {
        Kjør(new CacheRecord
        {
            Longitude = 10.7512345678, Latitude = 59.9112345678, Country = "Norway"
        });

        _context.DynamicProperties.Should().NotContainKey("_original_Longitude");
        _observasjon.Longitude.Should().Be(10.751235);
    }

    [Fact]
    public void Lagrer_ikke_original_naar_koordinaten_star_uendret()
    {
        Kjør(OsloPost());

        _context.DynamicProperties.Should().NotContainKey("_original_Longitude");
        _context.DynamicProperties.Should().NotContainKey("_original_Latitude");
    }

    // --- Datum --------------------------------------------------------------

    [Theory]
    [InlineData("ed50")]
    [InlineData("ED50")]
    [InlineData(" Ed50 ")]
    public void Transformerer_fra_ed50_og_lagrer_datumet(string datum)
    {
        var record = OsloPost();
        record.GeodeticDatum = datum;

        Kjør(record);

        _context.DynamicProperties.Should().ContainKey("_original_GeodeticDatum");
        _observasjon.Latitude.Should().NotBe(59.91, "ED50 ligger omtrent hundre meter unna");
        _observasjon.Latitude.Should().BeApproximately(59.91, 0.01);
    }

    [Fact]
    public void Transformerer_fra_euref89()
    {
        var record = OsloPost();
        record.GeodeticDatum = "euref89";

        Kjør(record);

        _context.DynamicProperties.Should().ContainKey("_original_GeodeticDatum");

        // ETRS89 og WGS 84 behandles som samme datum, så koordinaten står praktisk talt
        // stille.
        _observasjon.Latitude.Should().BeApproximately(59.91, 0.000001);
    }

    /// <summary>Andre datumverdier ignoreres og antas WGS 84 — dagens oppførsel.</summary>
    [Fact]
    public void Ignorerer_ukjent_datum()
    {
        var record = OsloPost();
        record.GeodeticDatum = "NAD83";

        Kjør(record);

        _context.DynamicProperties.Should().NotContainKey("_original_GeodeticDatum");
        _observasjon.Latitude.Should().Be(59.91);
    }

    // --- Trinn 2 og 3: UTM --------------------------------------------------

    [Fact]
    public void Bruker_utm_sone_33_direkte()
    {
        var resultat = Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, UTMsone = 33, UTMost = 262000, UTMnord = 6649000, Country = "Norway"
        });

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.MissingLongitudeLatitude);
        _observasjon.East.Should().Be(262000, "sone 33 er lagringssystemet, øst og nord brukes som de er");
        _observasjon.North.Should().Be(6649000);
        _observasjon.Longitude.Should().BeApproximately(10.75, 0.01);
    }

    [Fact]
    public void Transformerer_utm_fra_andre_soner_til_sone_33()
    {
        Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, UTMsone = 32, UTMost = 600000, UTMnord = 6650000, Country = "Norway"
        });

        _observasjon.East.Should().NotBe(600000, "sone 32 må regnes om");
        _observasjon.East.Should().BeGreaterThan(0);
        _observasjon.Longitude.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Artskart 2 godtar sone 30-38, men sonetabellen dekker 30-37. Sone 38 ender der i
    /// et unntak som feller hele nodeimporten; her avvises posten.
    /// </summary>
    [Fact]
    public void Avviser_utm_sone_38_i_stedet_for_a_kaste()
    {
        var resultat = Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, UTMsone = 38, UTMost = 500000, UTMnord = 6650000
        });

        resultat.IsAccepted.Should().BeFalse();
        resultat.ErrorContext.Should().Contain("38");
    }

    [Fact]
    public void Ignorerer_utm_uten_nordverdi()
    {
        var resultat = Kjør(new CacheRecord { NodeDatabaseID = 43, UTMsone = 33, UTMost = 262000 });

        // Faller gjennom til siste utvei og ender uten posisjon.
        _observasjon.HasErrors.Should().BeTrue();
        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
    }

    // --- Trinn 4: MGRS ------------------------------------------------------

    [Fact]
    public void Bruker_mgrs_naar_alt_annet_mangler()
    {
        var resultat = Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, MGRSNotation = "33VTG62004300", Country = "Norway"
        });

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        _observasjon.East.Should().Be(262000);
        _observasjon.North.Should().Be(6643000);
        _observasjon.CoordinatePrecisionInMeters.Should().Be(7, "rutestørrelsen gir presisjonen");
    }

    [Fact]
    public void Avviser_ugyldig_mgrs()
    {
        var resultat = Kjør(new CacheRecord { NodeDatabaseID = 43, MGRSNotation = "tull" });

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.CoordinateMissing);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.InvalidMgrsNotation);
        resultat.ErrorContext.Should().Contain("MGRS: tull");
    }

    /// <summary>
    /// Oppgir kilden presisjon selv, veier den tyngre enn rutestørrelsen.
    /// </summary>
    [Fact]
    public void Kildens_presisjon_vinner_over_mgrs_rutestorrelsen()
    {
        Kjør(new CacheRecord
        {
            NodeDatabaseID = 43,
            MGRSNotation = "33VTG62004300",
            CoordinateUncertaintyInMeters = "25",
            Country = "Norway"
        });

        _observasjon.CoordinatePrecisionInMeters.Should().Be(25);
    }

    // --- Trinn 5: kommunesentroide -----------------------------------------

    [Fact]
    public void Faller_tilbake_til_kommunesenteret()
    {
        var record = new CacheRecord { NodeDatabaseID = 43, County = "Oslo", Country = "Norway" };

        var resultat = Kjør(record);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        _observasjon.East.Should().BeGreaterThan(0);
        record.Notes.Should().StartWith("Mangler koordinat - satt til kommunesenter basert på navn:Oslo");
    }

    /// <summary>
    /// KOMMUNENS UTSTREKNING ER PRESISJONEN. Punktet er et senterpunkt, ikke en måling,
    /// og halve diagonalen er hvor feil det kan være. Verdien beregnes av
    /// <c>GeographyService</c>, og ble en stund regnet ut og kastet — målt på node 68
    /// sto 1 654 poster med tom presisjon der Artskart 2 har en verdi.
    ///
    /// PRESISJONEN ER DEL AV LOKALITETSNØKKELEN, så dette avgjør også hvilke
    /// <c>Location</c>-rader som opprettes.
    /// </summary>
    [Fact]
    public void Setter_kommunens_utstrekning_som_presisjon()
    {
        var record = new CacheRecord { NodeDatabaseID = 43, County = "Oslo", Country = "Norway" };

        Kjør(record);

        // Oslo-ruta i attrappen er 25 000 × 30 000 meter; halve diagonalen er ~19 500.
        _observasjon.CoordinatePrecisionInMeters.Should().BeInRange(15000, 25000);
    }

    /// <summary>
    /// Oppgir kilden en usikkerhet, er det den som gjelder — ikke kommunens
    /// utstrekning.
    /// </summary>
    [Fact]
    public void Kildens_usikkerhet_slar_kommunens_utstrekning()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            County = "Oslo",
            Country = "Norway",
            CoordinateUncertaintyInMeters = "250"
        };

        Kjør(record);

        _observasjon.CoordinatePrecisionInMeters.Should().Be(250);
    }

    /// <summary>
    /// «0» FRA KILDEN BETYR 0, IKKE UKJENT. Hjelpemetoden godtar bare tall over null —
    /// som i Artskart 2 — men Artskart 2 ender likevel med 0 i kolonnen: 1 519 poster
    /// på node 68 har presisjon 0, alle med «0» i kilden. Merknaden om at verdien ikke
    /// var brukbar legges igjen likevel.
    /// </summary>
    [Fact]
    public void Null_fra_kilden_blir_null_og_ikke_kommunens_utstrekning()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            County = "Oslo",
            Country = "Norway",
            CoordinateUncertaintyInMeters = "0"
        };

        Kjør(record);

        _observasjon.CoordinatePrecisionInMeters.Should().Be(0);
        _context.RecordIssues.Should().Contain(x => x.Remark == ImportIssueRemark.NoValidInteger);
    }

    /// <summary>
    /// Ubrukelig tekst er ikke 0 — da er presisjonen fortsatt ukjent, og kommunens
    /// utstrekning gjelder.
    /// </summary>
    [Fact]
    public void Ubrukelig_usikkerhet_gir_fortsatt_kommunens_utstrekning()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            County = "Oslo",
            Country = "Norway",
            CoordinateUncertaintyInMeters = "ukjent"
        };

        Kjør(record);

        _observasjon.CoordinatePrecisionInMeters.Should().BeInRange(15000, 25000);
    }

    /// <summary><c>UnknownFields["municipality"]</c> har forrang over <c>County</c>.</summary>
    [Fact]
    public void Foretrekker_municipality_fra_ukjente_felt_over_county()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            County = "Finnes ikke",
            UnknownFields = new Dictionary<string, string> { ["municipality"] = "Oslo" },
            Country = "Norway"
        };

        Kjør(record);

        record.Notes.Should().Contain("navn:Oslo");
    }

    [Fact]
    public void Foyer_kommunesentermeldingen_paa_eksisterende_notes()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43, County = "Oslo", Country = "Norway", Notes = "Fra kilden."
        };

        Kjør(record);

        // Merk at mellomrommet bakerst er borte: CacheRecord.Notes trimmer ved
        // tilordning, slik DarwinCoreProxy gjør i Artskart 2. Meldingen formateres med
        // et mellomrom før StateProvince, som her er tom.
        record.Notes.Should().Be("Fra kilden. Mangler koordinat - satt til kommunesenter basert på navn:Oslo");
    }

    // --- Ingen posisjon -----------------------------------------------------

    /// <summary>
    /// En observasjon uten sted er fortsatt en observasjon. Den importeres med
    /// nullkoordinat, feilrad og kvalitetsmerke — ikke avvist.
    /// </summary>
    [Fact]
    public void Importerer_post_uten_posisjon_med_merknad()
    {
        var record = new CacheRecord { NodeDatabaseID = 43, Locality = "et sted" };

        var resultat = Kjør(record);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.MissingLongitudeLatitude);
        _observasjon.HasErrors.Should().BeTrue();
        _observasjon.East.Should().Be(0);
        _observasjon.North.Should().Be(0);
        _observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        _observasjon.Errors.Should().ContainSingle(x => x.ValidationCode == RecordValidationCode.CoordinateMissing);
        record.Notes.Should().StartWith("Mangler koordinat/kommune");
    }

    /// <summary>
    /// Nullkoordinaten slipper unna både «nær Norge» og «traff et påkrevd område» —
    /// ellers ville hver slik post blitt avvist for å ligge i Atlanterhavet.
    /// </summary>
    [Fact]
    public void Post_uten_posisjon_avvises_ikke_av_grensekontrollene()
    {
        var resultat = Kjør(new CacheRecord { NodeDatabaseID = 43 });

        resultat.IsAccepted.Should().BeTrue();
        _observasjon.Location.Should().NotBeNull();
        _observasjon.Location!.Areas.Should().BeEmpty();
    }

    // --- Grensekontroller ---------------------------------------------------

    [Fact]
    public void Avviser_koordinat_utenfor_norge()
    {
        var resultat = Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, Longitude = 2.35, Latitude = 48.86 // Paris
        });

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.CoordinateOutOfBounds);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.NoHitOnSimpleNorwayBoundary);
    }

    /// <summary>
    /// Innenfor Norge-polygonet, men ikke i noe påkrevd område — altså i havet utenfor
    /// kommunegrensene.
    /// </summary>
    [Fact]
    public void Avviser_koordinat_uten_treff_paa_paakrevd_omrade()
    {
        var resultat = Kjør(new CacheRecord
        {
            NodeDatabaseID = 43, Longitude = 5.0, Latitude = 65.0, Country = "Norway"
        });

        resultat.IsAccepted.Should().BeFalse();
        resultat.NotificationCode.Should().Be(RecordNotificationCode.NoHitOnRequiredAreadatasets);
    }

    [Theory]
    [InlineData("Norway")]
    [InlineData("norge")]
    [InlineData("SVALBARD")]
    [InlineData("north sea")]
    [InlineData("0-unknown country")]
    public void Godtar_landnavnene_pa_hvitelisten(string land)
    {
        var record = OsloPost();
        record.Country = land;

        Kjør(record).IsAccepted.Should().BeTrue();
    }

    [Fact]
    public void Avviser_land_utenfor_hvitelisten()
    {
        var record = OsloPost();
        record.Country = "Sweden";

        var resultat = Kjør(record);

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.CoordinateOutOfBounds);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.CountryNameNotInNorway);
        resultat.ErrorContext.Should().Be("Sweden");
    }

    /// <summary>
    /// Sier kilden Jan Mayen, men koordinaten ligger i Oslo, er det koordinaten som er
    /// feil.
    /// </summary>
    [Fact]
    public void Avviser_jan_mayen_med_koordinat_et_annet_sted()
    {
        var record = OsloPost();
        record.StateProvince = "Jan Mayen";

        var resultat = Kjør(record);

        resultat.IsAccepted.Should().BeFalse();
        resultat.NotificationCode.Should()
            .Be(RecordNotificationCode.StateProvinceIsJanMayenButCoordinateElsewhere);
    }

    [Fact]
    public void Godtar_jan_mayen_med_koordinat_pa_jan_mayen()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            UTMsone = 33,
            UTMost = -400000,
            UTMnord = 8200000,
            StateProvince = "Jan Mayen",
            Country = "Jan Mayen"
        };

        Kjør(record).IsAccepted.Should().BeTrue();
    }

    // --- footprintWKT -------------------------------------------------------

    [Fact]
    public void Bruker_omrisset_som_lokalitetens_geometri()
    {
        var record = OsloPost();
        record.FootprintWkt =
            "POLYGON ((260000 6645000, 265000 6645000, 265000 6652000, 260000 6652000, 260000 6645000))";

        Kjør(record);

        _observasjon.GeometryWkt.Should().StartWith("POLYGON");
        _observasjon.Location!.Geometry.Should().BeOfType<NetTopologySuite.Geometries.Polygon>();
        _observasjon.CoordinatePrecisionInMeters.Should().BeGreaterThan(1000, "omrisset er flere kilometer bredt");
    }

    [Fact]
    public void Lager_punkt_som_geometri_naar_kilden_ikke_har_omriss()
    {
        Kjør(OsloPost());

        _observasjon.Location!.Geometry.Should().BeOfType<NetTopologySuite.Geometries.Point>();
        _observasjon.GeometryWkt.Should().BeNull();
    }

    /// <summary>
    /// Kommer omrisset i grader og posten mangler koordinat, brukes omrissets
    /// senterpunkt som posisjon.
    /// </summary>
    [Fact]
    public void Bruker_omrissets_senterpunkt_naar_posten_mangler_koordinat()
    {
        var record = new CacheRecord
        {
            NodeDatabaseID = 43,
            Country = "Norway",
            FootprintWkt = "POLYGON ((10.70 59.90, 10.80 59.90, 10.80 59.95, 10.70 59.95, 10.70 59.90))",
            UnknownFields = new Dictionary<string, string> { ["footprintSRS"] = "EPSG:4326" }
        };

        var resultat = Kjør(record);

        resultat.IsAccepted.Should().BeTrue();
        _observasjon.Longitude.Should().BeApproximately(10.75, 0.01);
        _observasjon.Latitude.Should().BeApproximately(59.925, 0.01);
    }

    [Fact]
    public void Ignorerer_ugyldig_omriss()
    {
        var record = OsloPost();
        record.FootprintWkt = "ikke wkt";

        Kjør(record).IsAccepted.Should().BeTrue();

        _observasjon.GeometryWkt.Should().BeNull();
        _observasjon.Location!.Geometry.Should().BeOfType<NetTopologySuite.Geometries.Point>();
    }

    // --- Lokalitetsnøkkel og buffer ----------------------------------------

    /// <summary>
    /// Node 1010 nøkles på <c>locationID</c> og ikke på koordinat — designvalget bak
    /// 143 455 overskuddsrader i <c>Location</c>.
    /// </summary>
    [Fact]
    public void Node_1010_nokler_lokaliteten_paa_locationId()
    {
        var steg = NyttSteg();
        var record = new CacheRecord
        {
            NodeDatabaseID = 1010, Longitude = 10.75, Latitude = 59.91,
            LocationID = " Et Sted ", Country = "Norway"
        };

        steg.ValidateAndImport(record, _observasjon, _context);

        _observasjon.Location!.LookupId.Should().Be("1010_etsted");
    }

    [Fact]
    public void Andre_noder_nokler_lokaliteten_paa_koordinat_og_presisjon()
    {
        var record = OsloPost();
        record.CoordinateUncertaintyInMeters = "25";

        Kjør(record);

        _observasjon.Location!.LookupId.Should().Be($"43_{_observasjon.East}_{_observasjon.North}_25");
    }

    [Fact]
    public void Gjenbruker_lokaliteten_for_samme_sted()
    {
        var steg = NyttSteg();

        var første = new ImportedObservation { NodeId = 43 };
        var andre = new ImportedObservation { NodeId = 43 };

        steg.ValidateAndImport(OsloPost(), første, new ImportContext());
        steg.ValidateAndImport(OsloPost(), andre, new ImportContext());

        andre.Location.Should().BeSameAs(første.Location, "samme nøkkel skal gi samme lokalitet");
    }

    /// <summary>
    /// Et sted som alt er avvist i denne kjøringen skal gi samme svar uten å gjøre
    /// områdeoppslaget på nytt.
    /// </summary>
    [Fact]
    public void Husker_avviste_steder()
    {
        var steg = NyttSteg();

        var paris = new CacheRecord { NodeDatabaseID = 43, Longitude = 2.35, Latitude = 48.86 };

        var første = steg.ValidateAndImport(paris, new ImportedObservation { NodeId = 43 }, new ImportContext());
        var andre = steg.ValidateAndImport(paris, new ImportedObservation { NodeId = 43 }, new ImportContext());

        andre.ValidationCode.Should().Be(første.ValidationCode);
        andre.NotificationCode.Should().Be(RecordNotificationCode.NoHitOnSimpleNorwayBoundary);
    }

    // --- Øvrige felt --------------------------------------------------------

    [Fact]
    public void Kutter_locality_til_hundre_tegn()
    {
        var record = OsloPost();
        record.Locality = new string('x', 200);

        Kjør(record);

        _observasjon.Locality.Should().HaveLength(100);
        _observasjon.Location!.Locality.Should().HaveLength(100);
    }

    [Fact]
    public void Kutter_locationId_til_femti_tegn()
    {
        var record = OsloPost();
        record.LocationID = new string('y', 80);

        Kjør(record);

        _observasjon.Location!.LocationId.Should().HaveLength(50);
    }

    [Fact]
    public void Tolker_presisjon_fra_kilden()
    {
        var record = OsloPost();
        record.CoordinateUncertaintyInMeters = "ca 50 meter";

        Kjør(record);

        _observasjon.CoordinatePrecisionInMeters.Should().Be(50);
    }

    [Fact]
    public void Lagrer_kildens_presisjon_naar_den_ikke_kunne_tolkes()
    {
        var record = OsloPost();
        record.CoordinateUncertaintyInMeters = "omtrent en stein";

        Kjør(record);

        _observasjon.CoordinatePrecisionInMeters.Should().BeNull();
        _context.DynamicProperties["_original_CoordinatePrecisionInMeters"].Should().Be("omtrent en stein");
        _context.RecordIssues.Should().ContainSingle(x => x.Term == "CoordinateUncertaintyInMeters");
    }
}

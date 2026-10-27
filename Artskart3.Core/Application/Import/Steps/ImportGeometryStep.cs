using System.Globalization;
using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;
using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Finner posisjonen til posten og lokaliteten den hører til. Port av Artskart 2s
/// <c>ImportGeometry</c>.
///
/// DET STØRSTE STEGET, og det som avgjør mest: en post uten brukbar posisjon er ikke en
/// observasjon noen kan finne i kartet. Fem av importens avvisningsgrunner kommer
/// herfra.
///
/// KOORDINATEN LETES ETTER I FEM TRINN, i denne rekkefølgen:
///
/// <list type="number">
/// <item>Lengde- og breddegrad fra kilden, transformert fra ED50 eller EUREF89 hvis
/// datumet sier det</item>
/// <item>UTM sone 33</item>
/// <item>UTM sone 30–37, transformert til sone 33</item>
/// <item>MGRS-rutereferanse — er den ugyldig, avvises posten</item>
/// <item>Senterpunktet i kommunen eller fylket, slått opp på navn</item>
/// </list>
///
/// Finner ingen av dem noe, importeres posten likevel med nullkoordinat, en feilrad og
/// <c>Tag.QualityIssue</c>. Det er et bevisst valg i Artskart 2: en observasjon uten
/// sted er fortsatt en observasjon.
///
/// NULLKOORDINAT SLIPPER UNNA GRENSEKONTROLLENE. Både «nær Norge» og «traff et påkrevd
/// område» hoppes over for en post uten koordinat — ellers ville alle blitt avvist for
/// å ligge i Atlanterhavet. Merk at sjekken gjøres på lengde- og breddegrad, mens
/// kontrollene gjelder øst/nord; det er slik Artskart 2 har det.
///
/// LOKALITETER DELES. Nøkkelen er <c>{node}_{øst}_{nord}_{presisjon}</c> — eller
/// <c>1010_{locationID}</c> for Artsobservasjoner, som er designvalget bak 143 455
/// overskuddsrader i <c>Location</c> (kapittel 4 i bugdokumentet). Steget bufrer både
/// lokaliteter og avvisninger per nøkkel, så områdeoppslaget gjøres én gang per sted.
///
/// SKRIVER INGENTING. Artskart 2 slår opp lokaliteten i basen her og lagrer den i et
/// eget steg til slutt. Her bygges den bare; fase 6 slår den opp mot
/// <c>Location.LookupId</c> og skriver den.
/// </summary>
public class ImportGeometryStep : IImportStep
{
    /// <summary>
    /// Omtrent én meter i grader. Avviker den beregnede koordinaten mer enn dette fra
    /// kildens, lagres kildens verdi som <c>_original_</c>.
    /// </summary>
    private const double Delta = 9e-6;

    /// <summary>Lokalitetsbufferen tømmes ved denne størrelsen. Tallet er Artskart 2s.</summary>
    private const int BufferGrense = 2500;

    /// <summary>
    /// Landnavn som regnes som Norge. Lista er Artskart 2s, med forkortelser og
    /// fremmedspråklige former slik kildene faktisk leverer dem.
    /// </summary>
    private static readonly HashSet<string> GyldigeLandnavn = new(StringComparer.Ordinal)
    {
        "svalbard and jan mayen",
        "0-unknown country",
        "north sea",
        "jan mayen",
        "norge",
        "norway",
        "bear island",
        "country not established",
        "n",
        "norvège",
        "norwegen",
        "unknown country",
        "no",
        "svalbard",
        "svalbard and jan may"
    };

    private readonly IGeographyService _geography;
    private readonly ICoordinateTransformer _transformer;

    private Dictionary<string, ImportedLocation> _lokaliteter = [];
    private readonly Dictionary<string, ImportRecordResult> _avvisteLokaliteter = [];

    public ImportGeometryStep(IGeographyService geography, ICoordinateTransformer transformer)
    {
        _geography = geography;
        _transformer = transformer;
    }

    public ImportStepType StepType => ImportStepType.Parallel;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        var avvisning = FinnKoordinat(record, observation, context, out var notificationCode, out var omriss);
        if (avvisning is not null) return avvisning;

        var nøkkel = LagLokalitetsnøkkel(record, observation);

        // Samme sted er avvist før i denne kjøringen — svaret er det samme.
        if (_avvisteLokaliteter.TryGetValue(nøkkel, out var tidligereAvvisning)) return tidligereAvvisning;

        if (_lokaliteter.TryGetValue(nøkkel, out var bufret))
        {
            observation.Location = bufret;
            context.Location = bufret;
        }
        else
        {
            var lokalitet = ByggLokalitet(record, observation, nøkkel, omriss, out var lokalitetsavvisning);

            if (lokalitetsavvisning is not null)
            {
                _avvisteLokaliteter[nøkkel] = lokalitetsavvisning;
                return lokalitetsavvisning;
            }

            observation.Location = lokalitet;
            context.Location = lokalitet;

            if (_lokaliteter.Count > BufferGrense) _lokaliteter = [];

            _lokaliteter[nøkkel] = lokalitet;
        }

        // Sier kilden Jan Mayen, men koordinaten ligger ikke der, er det koordinaten som
        // er feil — ikke navnet.
        if (string.Equals(record.StateProvince?.Trim(), "jan mayen", StringComparison.OrdinalIgnoreCase)
            && observation.Location!.Areas.All(x => x.Name != "Jan Mayen"))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds,
                RecordNotificationCode.StateProvinceIsJanMayenButCoordinateElsewhere,
                string.IsNullOrWhiteSpace(record.Country) ? "missing county" : record.Country);
        }

        // HasErrors her kommer fra nullkoordinaten. Posten importeres, men merket.
        return observation.HasErrors
            ? ImportRecordResult.Warn(notificationCode, string.Empty)
            : ImportRecordResult.Ok(notificationCode);
    }

    /// <summary>
    /// Trinnene som leter etter en koordinat. Returnerer en avvisning, eller
    /// <c>null</c> når posten kan gå videre.
    /// </summary>
    private ImportRecordResult? FinnKoordinat(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        out RecordNotificationCode notificationCode,
        out Geometry? omriss)
    {
        notificationCode = RecordNotificationCode.Ok;
        omriss = null;

        // Utenfor jorda er det ingenting å tolke.
        if (record.Latitude is > 90 or < -90 || record.Longitude is < -180 or > 180)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds,
                RecordNotificationCode.InvalidLongitudeLatitude,
                $"Not on earth: lon:{Tall(record.Longitude)} lat:{Tall(record.Latitude)}");
        }

        if (!string.IsNullOrWhiteSpace(record.Locality))
        {
            observation.Locality = ImportValueHelper.Left(record.Locality, 100);
        }

        var latLon = LagLatLon(record, context);
        int? presisjonFraOmriss = null;

        // Omrisset tolkes uansett om posten har koordinat — det er lokalitetens geometri.
        if (!string.IsNullOrWhiteSpace(record.FootprintWkt))
        {
            var footprintSrs = record.UnknownFields is not null
                               && record.UnknownFields.TryGetValue("footprintSRS", out var verdi)
                ? verdi
                : null;

            if (_geography.TryValidateFootprint(record.FootprintWkt, footprintSrs, 1, out var tolket))
            {
                observation.GeometryWkt = tolket!.Wkt;
                omriss = tolket.Geometry;
                presisjonFraOmriss = tolket.PrecisionInMeters;

                // Kom omrisset i grader og posten mangler koordinat, brukes omrissets
                // senterpunkt som posisjon.
                if (tolket.CentroidLatLon is { } senter && latLon.X == 0) latLon = senter;
            }
        }

        var utm33 = new GeoPoint(0, 0, CoordinateSystems.Etrs89UtmZone33N);
        int? presisjonFraMgrs = null;
        int? presisjonFraSentroide = null;

        if (latLon.IsNull)
        {
            notificationCode = RecordNotificationCode.MissingLongitudeLatitude;

            var utfall = FinnKoordinatUtenLatLon(
                record, observation, ref latLon, ref utm33, ref notificationCode,
                ref presisjonFraMgrs, ref presisjonFraSentroide);

            if (utfall is not null) return utfall;

            observation.East = (int)utm33.X;
            observation.North = (int)utm33.Y;
        }

        observation.Longitude = ImportValueHelper.SetDigitsForLatLon(latLon.X);
        observation.Latitude = ImportValueHelper.SetDigitsForLatLon(latLon.Y);

        // Presisjonen kommer fra kilden hvis den kan tolkes som tall, ellers fra det
        // koordinaten ble utledet av: MGRS-ruta, kommunens utstrekning eller omrisset.
        // Artskart 2 har én kilde for de tre — `coordinate.MetricCoordinatePrecision`,
        // satt av den grenen som laget koordinaten — og rekkefølgen her er den samme:
        // kilden først, så opphavet.
        var fraKilden = ImportValueHelper.ExtractIntegerIfPossible(
            record.CoordinateUncertaintyInMeters, "CoordinateUncertaintyInMeters", context.RecordIssues);

        // EN KILDE SOM OPPGIR «0» MENER 0, IKKE «UKJENT». Hjelpemetoden godtar bare tall
        // over null — som i Artskart 2 — og legger igjen en merknad om at verdien ikke
        // var brukbar. Men Artskart 2 ender likevel med 0 i kolonnen: målt på node 68
        // har 1 519 poster presisjon 0, alle med «0» i kilden. Nullen leses derfor ut
        // her, etter at merknaden er lagt igjen, slik at begge deler blir som før.
        //
        // Rekkefølgen er viktig: 0 fra kilden slår utstrekningen til kommunen. De 83
        // postene i samme node som får kommunens utstrekning, er de som ikke oppgir
        // noen usikkerhet i det hele tatt.
        if (fraKilden is null && ErEksaktNull(record.CoordinateUncertaintyInMeters)) fraKilden = 0;

        observation.CoordinatePrecisionInMeters =
            fraKilden ?? presisjonFraMgrs ?? presisjonFraSentroide ?? presisjonFraOmriss;

        LagreOpprinneligePosisjonsverdier(record, observation, context);

        // Har vi lengde- og breddegrad men ikke øst/nord, må de regnes ut.
        if (observation.North == 0 && !latLon.IsNull)
        {
            var omregnet = _transformer.Transform(latLon, CoordinateSystems.Etrs89UtmZone33N);

            observation.East = (int)Math.Round(omregnet.X, 0, MidpointRounding.ToEven);
            observation.North = (int)Math.Round(omregnet.Y, 0, MidpointRounding.ToEven);
        }

        // Landnavnet er det eneste som kan avsløre at posten hører til et annet land når
        // koordinaten mangler.
        if (!string.IsNullOrWhiteSpace(record.Country)
            && !GyldigeLandnavn.Contains(record.Country.ToLowerInvariant()))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds,
                RecordNotificationCode.CountryNameNotInNorway,
                record.Country);
        }

        return null;
    }

    /// <summary>Trinn 2 til 5: UTM, MGRS og kommunesentroide.</summary>
    private ImportRecordResult? FinnKoordinatUtenLatLon(
        CacheRecord record,
        ImportedObservation observation,
        ref GeoPoint latLon,
        ref GeoPoint utm33,
        ref RecordNotificationCode notificationCode,
        ref int? presisjonFraMgrs,
        ref int? presisjonFraSentroide)
    {
        // Sone 33 er allerede lagringssystemet — øst og nord brukes som de er.
        if (record.UTMsone == 33 && record.UTMnord > 0)
        {
            utm33 = new GeoPoint(record.UTMost, record.UTMnord, CoordinateSystems.Etrs89UtmZone33N);
            latLon = _transformer.Transform(utm33, CoordinateSystems.Wgs84LatLon);

            return null;
        }

        if (record.UTMnord > 0 && record.UTMsone is >= 30 and <= 38)
        {
            // Artskart 2 godtar sone 30-38 her, men sonetabellen dekker 30-37. En post i
            // sone 38 ender derfor i et unntak som feller hele nodeimporten. Her avvises
            // posten i stedet.
            if (record.UTMsone > 37)
            {
                return ImportRecordResult.Rejected(
                    RecordValidationCode.CoordinateOutOfBounds,
                    RecordNotificationCode.InvalidLongitudeLatitude,
                    $"UTM-sone {record.UTMsone} er utenfor sone 30-37");
            }

            var kilde = new GeoPoint(
                record.UTMost, record.UTMnord, CoordinateSystems.Etrs89UtmFromZone(record.UTMsone));

            latLon = _transformer.Transform(kilde, CoordinateSystems.Wgs84LatLon);
            utm33 = _transformer.Transform(kilde, CoordinateSystems.Etrs89UtmZone33N);

            return null;
        }

        if (!string.IsNullOrWhiteSpace(record.MGRSNotation))
        {
            var mgrs = MgrsHelper.IsValidMgrsNotation(record.MGRSNotation)
                ? MgrsHelper.MgrsToUtm(record.MGRSNotation)
                : null;

            if (mgrs is null)
            {
                notificationCode = RecordNotificationCode.InvalidMgrsNotation;

                return ImportRecordResult.Rejected(
                    RecordValidationCode.CoordinateMissing, notificationCode, MgrsFeilkontekst(record));
            }

            latLon = _transformer.Transform(mgrs.Point, CoordinateSystems.Wgs84LatLon);
            utm33 = _transformer.Transform(mgrs.Point, CoordinateSystems.Etrs89UtmZone33N);

            // Rutestørrelsen er presisjonen når kilden ikke oppgir noen.
            presisjonFraMgrs = mgrs.CoordinatePrecisionInMeters;

            return null;
        }

        // Siste utvei: kommunenavn. UnknownFields["municipality"] har forrang over County.
        var kommune = record.UnknownFields is not null
                      && record.UnknownFields.TryGetValue("municipality", out var fraUkjente)
                      && !string.IsNullOrWhiteSpace(fraUkjente)
            ? fraUkjente
            : record.County;

        if (!string.IsNullOrWhiteSpace(kommune)
            && _geography.TryGetMunicipalityCentroid(kommune, record.StateProvince, out var sentroide))
        {
            utm33 = sentroide!.Point;
            latLon = _transformer.Transform(utm33, CoordinateSystems.Wgs84LatLon);

            // Kommunens utstrekning er presisjonen: punktet er et senterpunkt, ikke en
            // måling, og halve diagonalen er hvor feil det kan være.
            presisjonFraSentroide = sentroide.PrecisionInMeters;

            var melding = $"Mangler koordinat - satt til kommunesenter basert på navn:{kommune} {record.StateProvince}";

            record.Notes = string.IsNullOrWhiteSpace(record.Notes) ? melding : record.Notes + " " + melding;

            return null;
        }

        // Ingen posisjon i det hele tatt. Posten importeres merket, ikke avvist.
        observation.HasErrors = true;
        record.Notes += $"Mangler koordinat/kommune {record.Country} {record.StateProvince} "
                        + $"{kommune} {record.Locality} ";

        observation.AddError(
            RecordValidationCode.CoordinateMissing,
            ImportValueHelper.Left($"{record.Country}|{record.StateProvince}|{kommune}|{record.Locality}", 400));

        observation.AddTag(Tag.QualityIssue);

        return null;
    }

    /// <summary>
    /// Lengde- og breddegrad fra kilden, transformert fra ED50 eller EUREF89 når datumet
    /// sier det. Andre datumverdier ignoreres og antas WGS 84 — det er Artskart 2s
    /// oppførsel, og grunn nok til at <c>_original_GeodeticDatum</c> lagres.
    /// </summary>
    private GeoPoint LagLatLon(CacheRecord record, ImportContext context)
    {
        var datum = record.GeodeticDatum?.Trim().ToLowerInvariant();

        var kildesystem = datum switch
        {
            "ed50" => CoordinateSystems.Ed50LatLon,
            "euref89" => CoordinateSystems.Etrs89LatLon,
            _ => CoordinateSystems.Wgs84LatLon
        };

        if (kildesystem == CoordinateSystems.Wgs84LatLon)
        {
            return new GeoPoint(record.Longitude, record.Latitude, CoordinateSystems.Wgs84LatLon);
        }

        context.DynamicProperties[ImportContext.OriginalValuePrefix + "GeodeticDatum"] =
            record.GeodeticDatum!.Trim();

        return _transformer.Transform(
            new GeoPoint(record.Longitude, record.Latitude, kildesystem), CoordinateSystems.Wgs84LatLon);
    }

    /// <summary>
    /// Avviker det importen kom fram til fra det kilden sendte, lagres kildens verdi
    /// under <c>_original_</c> — og postens felt overskrives, slik at senere steg og
    /// annotasjoner ser den tolkede verdien.
    /// </summary>
    private static void LagreOpprinneligePosisjonsverdier(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        if (Math.Abs(observation.Longitude - record.Longitude) > Delta)
        {
            context.DynamicProperties[ImportContext.OriginalValuePrefix + "Longitude"] =
                record.Longitude.ToString(NumberFormatInfo.InvariantInfo);

            record.Longitude = observation.Longitude;
        }

        if (Math.Abs(observation.Latitude - record.Latitude) > Delta)
        {
            context.DynamicProperties[ImportContext.OriginalValuePrefix + "Latitude"] =
                record.Latitude.ToString(NumberFormatInfo.InvariantInfo);

            record.Latitude = observation.Latitude;
        }

        var presisjon = observation.CoordinatePrecisionInMeters;
        var kildensPresisjon = record.CoordinateUncertaintyInMeters;

        if ((presisjon is null && !string.IsNullOrWhiteSpace(kildensPresisjon))
            || (presisjon is not null
                && presisjon.Value.ToString(CultureInfo.InvariantCulture) != kildensPresisjon))
        {
            context.DynamicProperties[ImportContext.OriginalValuePrefix + "CoordinatePrecisionInMeters"] =
                kildensPresisjon ?? string.Empty;

            record.CoordinateUncertaintyInMeters = presisjon?.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Bygger lokaliteten og kontrollerer at den ligger i Norge. Lokaliteten kommer
    /// tilbake uansett — Artskart 2 bruker den i feilmeldingen.
    /// </summary>
    private ImportedLocation ByggLokalitet(
        CacheRecord record,
        ImportedObservation observation,
        string nøkkel,
        Geometry? omriss,
        out ImportRecordResult? avvisning)
    {
        var lokalitet = new ImportedLocation
        {
            LookupId = nøkkel,
            LocationId = ImportValueHelper.Left(record.LocationID ?? string.Empty, 50),
            NodeId = observation.NodeId,
            East = observation.East,
            North = observation.North,
            Latitude = observation.Latitude,
            Longitude = observation.Longitude,
            CoordinatePrecision = observation.CoordinatePrecisionInMeters,

            // Kolonnen tar 1000 tegn, mens observasjonens eget felt er kuttet til 100.
            Locality = observation.Locality,

            // Omrisset hvis kilden hadde et, ellers punktet. Geometrien er alt parset av
            // geografitjenesten — den skal ikke gjennom tekst én gang til.
            Geometry = omriss ?? Punkt(observation.East, observation.North)
        };

        // En post uten koordinat slipper unna grensekontrollene. Sjekken gjøres på
        // lengde- og breddegrad, mens kontrollene gjelder øst/nord — slik Artskart 2
        // har det.
        var harKoordinat = !GeoPoint.IsNullPosition(observation.Longitude, observation.Latitude);

        if (harKoordinat && !_geography.CloseToNorway(observation.East, observation.North))
        {
            avvisning = ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds,
                RecordNotificationCode.NoHitOnSimpleNorwayBoundary,
                "Coordinate out of bounds. Coordinate Not inside boundary of Norway");

            return lokalitet;
        }

        var områder = _geography.GetAreasForCoordinate(observation.East, observation.North);

        // Tom liste betyr at ingen påkrevd områdetype traff — altså utenfor Norge.
        // Artskart 2 har i tillegg en gren for at områdeoppslaget feilet med SQL-feil,
        // med en teller som kaster etter hundre poster. Den finnes ikke her: oppslaget
        // er i minnet, og har ikke noe nettverk å feile på.
        if (harKoordinat && områder.Count == 0)
        {
            avvisning = ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds,
                RecordNotificationCode.NoHitOnRequiredAreadatasets,
                $"No hit on required areas with {observation.ProxyId} - coord {nøkkel}");

            return lokalitet;
        }

        foreach (var område in områder)
        {
            lokalitet.Areas.Add(new LocationArea(område.Id, område.AreaTypeId, område.Fid, område.Name));
        }

        avvisning = null;

        return lokalitet;
    }

    /// <summary>
    /// Nøkkelen lokaliteter deles på. Port av <c>Location.GenerateId</c>.
    ///
    /// NODE 1010 NØKLES PÅ <c>locationID</c>, ikke på koordinat. Det er et dokumentert
    /// designvalg med en målt følge: 143 455 overskuddsrader i <c>Location</c>, fordi
    /// samme sted rapportert to ganger får to nøkler. Se kapittel 4 i bugdokumentet.
    /// </summary>
    private static string LagLokalitetsnøkkel(CacheRecord record, ImportedObservation observation)
    {
        var locationId = record.LocationID ?? string.Empty;

        if (record.NodeDatabaseID == 1010 && !string.IsNullOrWhiteSpace(locationId))
        {
            return ImportValueHelper.Left(
                "1010_" + locationId.Trim().ToLowerInvariant().Replace(" ", string.Empty), 50)!;
        }

        var presisjon = observation.CoordinatePrecisionInMeters?.ToString(CultureInfo.InvariantCulture)
                        ?? string.Empty;

        return $"{record.NodeDatabaseID}_{observation.East}_{observation.North}_{presisjon}";
    }

    private static Point Punkt(int east, int north) =>
        new(new Coordinate(east, north)) { SRID = CoordinateSystems.Etrs89UtmZone33N };

    private static string MgrsFeilkontekst(CacheRecord record) =>
        "Missing lon/lat and utm, and have invalid Mgrs notation. "
        + $"Lon: {Tall(record.Longitude)} _ Lat: {Tall(record.Latitude)} _ "
        + $"UtmZone: {record.UTMsone} _ UtmNorth: {record.UTMnord} _ UtmEast: {record.UTMost} _ "
        + $"MGRS: {record.MGRSNotation}";

    /// <summary>
    /// Om kilden oppgir eksakt null som usikkerhet. Da er presisjonen 0 og ikke
    /// ukjent — se der den brukes for hvorfor det skilles.
    /// </summary>
    private static bool ErEksaktNull(string? verdi) =>
        !string.IsNullOrWhiteSpace(verdi)
        && int.TryParse(verdi.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tall)
        && tall == 0;

    private static string Tall(double verdi) => verdi.ToString(ImportTextFormat.Culture);
}

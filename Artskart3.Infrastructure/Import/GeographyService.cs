using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;
using Coordinate = NetTopologySuite.Geometries.Coordinate;
using Geometry = NetTopologySuite.Geometries.Geometry;
using Point = NetTopologySuite.Geometries.Point;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Geografioppslagene importen gjør. Port av de fire metodene importstien bruker i
/// Artskart 2s <c>GeographyService</c>.
///
/// OMRÅDENE LIGGER I MINNET, IKKE I EN SPØRRING PER POST.
///
/// Artskart 2 kjører én romlig spørring mot <c>Area</c> for hver koordinat, med en
/// retry-løkke som sover et sekund og prøver fem ganger når den feiler. Det er
/// kostnaden i kostnadsmodellen: 4,6 ms per ny lokalitet. Over 5,1 millioner
/// lokaliteter blir det omtrent seks og en halv time — bare på dette oppslaget.
///
/// Områdedataene er små nok til å slippe det: 4 224 rader, 82,9 MB geometri. Lastet én
/// gang i et romlig søketre (<c>STRtree</c>) med forberedte geometrier er oppslaget
/// billig, og retry-løkken er unødvendig — det finnes ikke noe nettverk å feile på.
///
/// MÅLT MOT <c>Artskart3IndexProdLikeTestMigrations</c>, 10 000 oppslag spredt over
/// Norge (<c>GeographyServiceSmokeTests</c>):
///
/// <list type="bullet">
/// <item>Lasting og bygging av treet: 2,3 s, én spørring</item>
/// <item>Minne: 319 MB for 82,9 MB WKB</item>
/// <item>0,134 ms per oppslag, mot Artskart 2s 4,6 ms — 34 ganger raskere</item>
/// <item>Uten forberedte geometrier: 1,58 ms. Kantindeksen er altså det som gjør
/// forskjellen, ikke søketreet alene</item>
/// </list>
///
/// For 5,1 millioner nye lokaliteter er det omtrent elleve minutter mot seks og en
/// halv time. Prisen er 319 MB i workerprosessen, og den er verdt det for en jobb som
/// uansett kjører alene.
///
/// LASTES VED FØRSTE BRUK, ikke i konstruktøren. Importen er en jobb som startes, ikke
/// noe som kjører hele tiden; en worker som ikke importerer skal ikke bruke minnet.
/// Tjenesten er tenkt som singleton per prosess, og lastingen er tråd-trygg.
///
/// ALT ER 25833. Artskart 2 bygger punktene sine som 32633 fordi det er det lagringen
/// var merket med; etter <c>RelabelGeometrySridTo25833</c> er alt 25833, og
/// koordinatene er de samme tallene.
/// </summary>
public class GeographyService : IGeographyService
{
    /// <summary>
    /// Grovt polygon rundt Norge med omliggende havområder. WKT-en er kopiert ordrett
    /// fra Artskart 2 (<c>caNorsk</c>) — den er håndtegnet, og en «opprydding» ville
    /// flyttet grensen for hva som regnes som nær Norge.
    /// </summary>
    private const string NorgePolygonWkt =
        "POLYGON ((1333484 8210294, 1190568 8688625, 783698 9336932, 503262 9308580, "
        + "-425708 8202515, -556834 8020884, -565075 8002376, -266966 6343792, "
        + "-231552 6279616, -149564 6302957, 138121 6411462, 307178 6534437, "
        + "1114892 7797551, 1264943 8070294, 1333484 8210294))";

    private readonly IAreaGeometrySource _source;
    private readonly ICoordinateTransformer _transformer;
    private readonly ILogger<GeographyService> _logger;
    private readonly Lock _låse = new();

    private readonly Geometry _norge;
    private readonly WKTReader _wktReader = new(NtsGeometryServices.Instance);

    private STRtree<IndeksertOmrade>? _tre;
    private Dictionary<(string Municipality, string County), CentroidHit>? _sentroider;

    public GeographyService(
        IAreaGeometrySource source,
        ICoordinateTransformer transformer,
        ILogger<GeographyService> logger)
    {
        _source = source;
        _transformer = transformer;
        _logger = logger;

        _norge = _wktReader.Read(NorgePolygonWkt);
        _norge.SRID = CoordinateSystems.Etrs89UtmZone33N;
    }

    public IReadOnlyList<AreaGeometry> GetAreasForCoordinate(int east, int north)
    {
        var punkt = Punkt(east, north);
        var tre = HentTre();

        // Søketreet gir kandidater ut fra omsluttende rektangler; omrisset må testes
        // presist etterpå.
        //
        // BARE GJELDENDE OMRÅDER. Artskart 2s oppslag filtrerer ikke — verken
        // `TryAreasForCoordinate`, `ImportGeometry` eller `StoreLocation` — men dataene
        // dens inneholder likevel bare gjeldende områder i `LocationAreas`. Uten
        // filteret her skrev importen 444 ekstra rader for node 43s 133 lokaliteter
        // (219 historiske kommuner, 225 historiske fylker), og `Area.ObservationCount`
        // ble 48 629 mot Artskart 2s 20 160 for samme node.
        //
        // Det er bare tellingen som ser forskjellen: `ObservationEntityIndex` filtrerer
        // på `IsCurrent = 1` selv, og stemte rad for rad begge veier. Filteret ligger
        // derfor her, ved kilden, slik at `LocationAreas` og tellingene blir enige.
        //
        // Sentroidtabellen under laster fortsatt historiske områder med vilje: en post
        // som navngir en kommune som ikke finnes lenger skal fortsatt kunne plasseres.
        var treff = tre.Query(punkt.EnvelopeInternal)
            .Where(x => x.Inneholder(punkt))
            .Select(x => x.Område)
            .Where(x => x.IsCurrent)
            .ToList();

        // Ingen påkrevd områdetype blant treffene betyr utenfor Norge, og da skal
        // ingenting returneres — heller ikke verneområdet koordinaten tilfeldigvis
        // ligger i.
        //
        // SJEKKEN KOMMER ETTER FILTERET, så kravet er at posten ligger i et *gjeldende*
        // påkrevd område. En koordinat som bare treffer en historisk kommune er utenfor
        // dagens Norge, og det er riktig svar.
        if (!treff.Any(x => x.IsRequired)) return [];

        return treff
            .OrderBy(x => x.AreaTypeId)
            .ThenBy(x => x.Fid, StringComparer.Ordinal)
            .ToList();
    }

    public bool CloseToNorway(int east, int north) => _norge.Contains(Punkt(east, north));

    public bool TryValidateFootprint(
        string? footprintWkt,
        string? footprintSrs,
        int decimals,
        out FootprintGeometry? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(footprintWkt)) return false;

        if (TryTolk(footprintWkt, footprintSrs, decimals, out result)) return true;

        // Artskart 2s andre forsøk: noen kilder leverer et polygon som ikke er lukket.
        // Førstepunktet limes inn før avslutningsparentesen og omrisset prøves igjen.
        var lukket = LukkRing(footprintWkt);

        return lukket is not null && TryTolk(lukket, footprintSrs, decimals, out result);
    }

    public bool TryGetMunicipalityCentroid(string? municipality, string? county, out CentroidHit? result)
    {
        result = null;

        // «&» skrives «og» i områdenavnene. Artskart 2 gjør samme bytte på begge sider.
        var kommune = Normaliser(municipality);
        var fylke = Normaliser(county);

        if (string.IsNullOrEmpty(kommune)) return false;

        var sentroider = HentSentroider();

        // 1. Kommune og fylke stemmer.
        if (sentroider.TryGetValue((kommune, fylke), out var treff))
        {
            result = treff with { MatchedOn = "kommune+fylke" };
            return true;
        }

        // 2. Kommunenavnet alene, uansett fylke. Første treff vinner — ordboken har
        //    ingen definert rekkefølge, så for et kommunenavn som finnes i to fylker er
        //    utfallet i praksis vilkårlig. Dagens oppførsel.
        var påKommune = sentroider.FirstOrDefault(x => x.Key.Municipality == kommune);
        if (påKommune.Value is not null)
        {
            result = påKommune.Value with { MatchedOn = "kommunenavn" };
            return true;
        }

        // 3. Fylket alene.
        if (sentroider.TryGetValue((string.Empty, fylke), out var fylkesTreff))
        {
            result = fylkesTreff with { MatchedOn = "fylke" };
            return true;
        }

        // 4. Byttet om: kommunenavnet står i fylkesfeltet og omvendt. Vanlig nok i
        //    kildedata at Artskart 2 prøver det.
        if (sentroider.TryGetValue((fylke, kommune), out var byttetTreff))
        {
            result = byttetTreff with { MatchedOn = "byttet om" };
            return true;
        }

        var påFylkeSomKommune = sentroider.FirstOrDefault(x => x.Key.Municipality == fylke);
        if (påFylkeSomKommune.Value is not null)
        {
            result = påFylkeSomKommune.Value with { MatchedOn = "fylkesfeltet som kommunenavn" };
            return true;
        }

        // 5. Kommunefeltet som fylkesnavn.
        if (sentroider.TryGetValue((string.Empty, kommune), out var kommuneSomFylke))
        {
            result = kommuneSomFylke with { MatchedOn = "kommunefeltet som fylkesnavn" };
            return true;
        }

        return false;
    }

    private bool TryTolk(string wkt, string? footprintSrs, int decimals, out FootprintGeometry? result)
    {
        result = null;

        Geometry geometri;

        try
        {
            geometri = _wktReader.Read(wkt);
        }
        catch (Exception e) when (e is ParseException or ArgumentException or FormatException)
        {
            return false;
        }

        // TOPOLOGISK UGYLDIGE OMRISS SKAL BEHOLDES, IKKE AVVISES.
        //
        // Her sto tidligere `|| !geometri.IsValid`. Det er NTS sin vurdering, ikke SQL
        // Servers `STIsValid` som Artskart 2 bruker, og de er ikke enige om
        // selvkryssende polygoner. Paritetsriggen målte følgen på gullsett medium:
        // 37 lokaliteter endte med et PUNKT i Artskart 3 der Artskart 2 har et polygon
        // på opptil 4 994 koordinater. Omrisset falt bort, og lokaliteten ble stedfestet
        // av koordinaten alene — uten at noe ble logget.
        //
        // Artskart 2 avviser ikke; den reparerer. For de 39 omrissene Artskart 3 lagret
        // som ugyldige er `MakeValid()` byte-identisk med det Artskart 2 har lagret, i
        // alle 39. Reparasjonen gjøres derfor på skrivesiden, av SQL Server, der den
        // reproduserer Artskart 2 eksakt — se `PublishSql`.
        //
        // Et tomt omriss er fortsatt ingen geometri og gir `false`.
        if (geometri.IsEmpty) return false;

        GeoPoint? sentroide = null;

        if (ErGrader(geometri, footprintSrs))
        {
            // Senterpunktet i grader hentes FØR transformasjonen — kallstedet bruker det
            // som observasjonens lengde- og breddegrad når posten mangler koordinat.
            var senter = geometri.Centroid;
            sentroide = new GeoPoint(senter.X, senter.Y, CoordinateSystems.Wgs84LatLon);

            geometri.SRID = CoordinateSystems.Wgs84LatLon;
            geometri = _transformer.TransformGeometry(
                geometri, CoordinateSystems.Wgs84LatLon, CoordinateSystems.Etrs89UtmZone33N, 2);
        }
        else
        {
            geometri.SRID = CoordinateSystems.Etrs89UtmZone33N;
        }

        if (decimals != -1) RundAv(geometri, decimals);

        result = new FootprintGeometry(
            geometri,
            geometri.AsText(),
            RadiusAvOmriss(geometri),
            sentroide);

        return true;
    }

    /// <summary>
    /// Er omrisset oppgitt i lengde- og breddegrad? To kjennetegn, begge Artskart 2s:
    /// kilden sier <c>EPSG:4326</c>, eller førstepunktets nordverdi er under 1000 —
    /// en UTM-northing i Norge er over seks millioner, så en verdi på 59 er grader.
    /// </summary>
    private static bool ErGrader(Geometry geometri, string? footprintSrs)
    {
        if (string.Equals(footprintSrs, "EPSG:4326", StringComparison.OrdinalIgnoreCase)) return true;

        var første = geometri.Coordinates.FirstOrDefault();

        return første is not null && første.Y < 1000;
    }

    /// <summary>
    /// Halve diagonalen i det omsluttende rektangelet, slik Artskart 2 regner
    /// presisjonen til et omriss. Taket på 2 000 000 000 er deres — det fanger en
    /// geometri med vrange koordinater før tallet renner over.
    /// </summary>
    private static int RadiusAvOmriss(Geometry geometri)
    {
        var omriss = geometri.EnvelopeInternal;
        var bredde = Math.Abs(omriss.MaxX - omriss.MinX);
        var høyde = Math.Abs(omriss.MaxY - omriss.MinY);
        var radius = 0.5 * Math.Sqrt((bredde * bredde) + (høyde * høyde));

        return radius < 2000000000 ? (int)radius : 2000000000;
    }

    private static void RundAv(Geometry geometri, int decimals)
    {
        foreach (var koordinat in geometri.Coordinates)
        {
            koordinat.X = Math.Round(koordinat.X, decimals, MidpointRounding.ToEven);
            koordinat.Y = Math.Round(koordinat.Y, decimals, MidpointRounding.ToEven);
        }

        geometri.GeometryChanged();
    }

    /// <summary>
    /// Limer førstepunktet inn før avslutningsparentesen, slik Artskart 2 gjør når
    /// første tolkning feiler.
    /// </summary>
    private static string? LukkRing(string wkt)
    {
        if (!wkt.Contains("))", StringComparison.Ordinal)) return null;

        var deler = wkt.Split(',');
        var førstePunkt = deler[0].Split('(').LastOrDefault();

        if (string.IsNullOrWhiteSpace(førstePunkt)) return null;

        return wkt.Replace("))", "," + førstePunkt + "))", StringComparison.Ordinal);
    }

    private static string Normaliser(string? navn)
    {
        if (string.IsNullOrEmpty(navn)) return string.Empty;

        var uten = navn.Contains('&', StringComparison.Ordinal) ? navn.Replace("&", "og") : navn;

        return uten.ToLowerInvariant().Trim();
    }

    private static Point Punkt(int east, int north) =>
        new(new Coordinate(east, north)) { SRID = CoordinateSystems.Etrs89UtmZone33N };

    private STRtree<IndeksertOmrade> HentTre()
    {
        if (_tre is not null) return _tre;

        lock (_låse)
        {
            if (_tre is not null) return _tre;

            Last();

            return _tre!;
        }
    }

    private Dictionary<(string Municipality, string County), CentroidHit> HentSentroider()
    {
        if (_sentroider is not null) return _sentroider;

        lock (_låse)
        {
            if (_sentroider is not null) return _sentroider;

            Last();

            return _sentroider!;
        }
    }

    /// <summary>
    /// Laster områdene og bygger både søketreet og sentroidordboken. Kalt under lås.
    /// </summary>
    private void Last()
    {
        var klokke = System.Diagnostics.Stopwatch.StartNew();
        var områder = _source.LoadAll();

        var tre = new STRtree<IndeksertOmrade>();

        foreach (var område in områder)
        {
            if (område.Geometry.IsEmpty) continue;

            tre.Insert(område.Geometry.EnvelopeInternal, new IndeksertOmrade(område));
        }

        // STRtree må bygges før første spørring, og er uforanderlig etterpå.
        tre.Build();

        _sentroider = ByggSentroider(områder);
        _tre = tre;

        klokke.Stop();

        _logger.LogInformation(
            "Lastet {AreaCount} områder og {CentroidCount} sentroider til minnet på {ElapsedMs} ms",
            områder.Count, _sentroider.Count, klokke.ElapsedMilliseconds);
    }

    /// <summary>
    /// Bygger oppslaget fra navn til senterpunkt. Kommuner nøkles på
    /// (kommune, fylke), fylker på (tom, fylke) — samme to former Artskart 2 bruker.
    ///
    /// GJELDENDE OMRÅDE VINNER. Kommunenavn gjenbrukes gjennom kommunereformene, og
    /// uten denne regelen ville en sammenslått kommune fra 1960 kunne overskrive dagens.
    ///
    /// EN KOMMUNE UTEN GJENKJENT FYLKE TAS MED MED TOMT FYLKESNAVN. Artskart 2 slår opp
    /// fylket på <c>ParentFid</c> og kaller <c>.Name</c> på resultatet uten å sjekke for
    /// null — finnes ikke fylket, kaster hele byggingen. Her får kommunen tomt
    /// fylkesnavn i stedet, slik at de øvrige kommunene fortsatt kan slås opp.
    /// </summary>
    private static Dictionary<(string Municipality, string County), CentroidHit> ByggSentroider(
        IReadOnlyList<AreaGeometry> områder)
    {
        var fylkerPåFid = områder
            .Where(x => x.AreaTypeId == 2)
            .GroupBy(x => x.Fid, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var resultat = new Dictionary<(string, string), CentroidHit>();
        var erGjeldende = new Dictionary<(string, string), bool>();

        void LeggTil((string, string) nøkkel, AreaGeometry område)
        {
            var treff = Sentroide(område);

            if (erGjeldende.TryGetValue(nøkkel, out var fantesGjeldende))
            {
                // Finnes nøkkelen fra før, overskrives den bare av et gjeldende område.
                if (!område.IsCurrent || fantesGjeldende) return;
            }

            resultat[nøkkel] = treff;
            erGjeldende[nøkkel] = område.IsCurrent;
        }

        foreach (var kommune in områder.Where(x => x.AreaTypeId == 1))
        {
            var fylkesnavn = kommune.ParentFid is not null
                             && fylkerPåFid.TryGetValue(kommune.ParentFid, out var fylke)
                ? Normaliser(fylke.Name)
                : string.Empty;

            LeggTil((Normaliser(kommune.Name), fylkesnavn), kommune);
        }

        foreach (var fylke in områder.Where(x => x.AreaTypeId == 2))
        {
            LeggTil((string.Empty, Normaliser(fylke.Name)), fylke);
        }

        return resultat;
    }

    /// <summary>
    /// Ett område i søketreet, med en forberedt geometri som bygges første gang noen
    /// faktisk tester mot det.
    ///
    /// HVORFOR: et punkt-i-polygon-test mot et fylke med titusenvis av hjørner går
    /// gjennom alle kantene. <c>PreparedGeometry</c> bygger en intern kantindeks én
    /// gang, og gjør testene etterpå logaritmiske. Målt på 10 000 oppslag spredt over
    /// Norge: 1,58 ms per oppslag uten, se klassekommentaren for tallet med.
    ///
    /// HVORFOR LAT: forberedelsen koster både tid og minne per område, og 3 394 av de
    /// 4 224 områdene er verneområder som de færreste koordinatene er i nærheten av.
    /// Lat bygging betyr at bare de områdene som faktisk testes mot betaler.
    ///
    /// To tråder kan bygge den samtidig. Det er ufarlig: resultatet er det samme, og
    /// den ene tilordningen overskriver den andre.
    /// </summary>
    private sealed class IndeksertOmrade(AreaGeometry område)
    {
        private IPreparedGeometry? _forberedt;

        public AreaGeometry Område { get; } = område;

        public bool Inneholder(Geometry punkt)
        {
            _forberedt ??= PreparedGeometryFactory.Prepare(Område.Geometry);

            return _forberedt.Intersects(punkt);
        }
    }

    private static CentroidHit Sentroide(AreaGeometry område)
    {
        var senter = område.Geometry.Centroid;

        // Avrundingen er Artskart 2s: heltall meter. Senterpunktet i en kommune trenger
        // ikke centimeter.
        var punkt = new GeoPoint(
            Math.Round(senter.X, 0),
            Math.Round(senter.Y, 0),
            CoordinateSystems.Etrs89UtmZone33N);

        return new CentroidHit(punkt, RadiusAvOmriss(område.Geometry), "ukjent");
    }
}

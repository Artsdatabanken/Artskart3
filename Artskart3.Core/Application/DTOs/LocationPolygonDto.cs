namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Geometrien til en lokasjon.
///
/// NAVNET ER FOR SNEVERT — EGEN TASK PÅ OMDØPING
/// Typen bærer også LineString og MultiLineString: transekter,
/// elvestrekninger og kystlinjer. De er reelle geografiske utstrekninger på
/// linje med polygonene, og 2 101 lokasjoner med 62 764 observasjoner falt
/// tidligere stille ut av kartet fordi filteret bare slapp gjennom Polygon og
/// MultiPolygon.
///
/// Å hente dem med feil navn er bedre enn å ikke hente dem. Både typenavnet,
/// WktPolygon og endepunktet LocationPolygons skal byttes, men det er en
/// API-endring mot frontend og hører til sin egen task.
/// </summary>
public class LocationPolygonDto
{
    public int LocationId { get; set; }

    public string? Locality { get; set; }

    /// <summary>
    /// WKT for geometrien. Kan være POLYGON, MULTIPOLYGON, LINESTRING eller
    /// MULTILINESTRING — se klassens merknad om navnet.
    ///
    /// NYTTELASTEN DOMINERES AV NOEN FÅ GEOMETRIER — MULIG FORENKLING SENERE
    /// Målt 23.10.2026 mot Artskart3IndexProdLikeTestMigrations, uten filter, på
    /// oslo-utsnittet (185 x 103 km, det største zoom 11 kan gi):
    ///
    ///   4 495 geometrier, 6,27 MB, 111-156 ms
    ///   WKT-lengde: p50 286 tegn, p90 1 642, p99 22 276, største 253 949
    ///   de 100 største av 4 495 utgjør 52,7 % av alle WKT-tegn
    ///
    /// Verste enkelttilfelle ligger i kristiansand-utsnittet: «Sogneelva», 693 600
    /// tegn over 28 331 punkter — 43 % av hele svaret på 1,6 MB, for ÉN geometri.
    /// Det er elver og naturreservater lagret som tett linjeverk; medianen er en
    /// liten geometri på 286 tegn.
    ///
    /// TIDEN ER IKKE PROBLEMET, STØRRELSEN ER DET. Svaret serveres fra
    /// PolygonLocationStore og bruker rundt 40 ms i snitt. En Douglas-Peucker-
    /// forenkling med toleranse styrt av zoomnivået er den åpenbare veien videre:
    /// 28 331 punkter for én elv er langt mer enn det som lar seg vise på zoom 11,
    /// mens median-geometrien ikke trenger behandling i det hele tatt. Gevinsten
    /// ligger altså i halen, ikke i antallet.
    ///
    /// Velges den veien, MÅ forenklingen skje likt i både databasestien i
    /// SearchRepository og minnelageret i PolygonLocationStore. Se
    /// LocationGeometryRules for hvorfor to utgaver av samme regel er en felle —
    /// og for to tilfeller der det faktisk gikk galt.
    ///
    /// IKKE GJORT, OG IKKE MÅLT MOT KOMPRIMERING: tallene over er ukomprimerte
    /// (RawContentLength), og WKT komprimerer godt. Serverer API-et gzip, er
    /// trådkostnaden lavere enn MB-tallene antyder. Mål det først — det kan være
    /// at komprimering alene er nok.
    /// </summary>
    public string WktPolygon { get; set; } = null!;

    public int ObservationCount { get; set; }
}

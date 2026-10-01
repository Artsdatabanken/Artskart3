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
    /// </summary>
    public string WktPolygon { get; set; } = null!;

    public int ObservationCount { get; set; }
}

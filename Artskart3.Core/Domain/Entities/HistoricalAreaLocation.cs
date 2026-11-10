namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Kobling mellom et historisk område (Area med IsCurrent = 0) og lokasjonene som
/// ligger i det.
///
/// LocationAreas har bare koblinger for gjeldende områder, og ObservationEntityIndex
/// er utledet av den. Historiske fylker og kommuner søkes sjelden, så koblingene ligger
/// i en egen tabell i stedet for å gjøre LocationAreas og indeksen større for alle
/// andre spørringer. Fylles av Scripts/BuildHistoricalAreaLocations.sql med samme regel
/// som LocationAreas: STIntersects mellom Location.Geometry og Area.WktPolygon.
///
/// Ingen fremmednøkler: Location og Area eies av prosessen som fyller indeksdatabasen,
/// og en nøkkel herfra ville hindret den i å slette rader.
/// </summary>
public class HistoricalAreaLocation
{
    public int AreaId { get; set; }

    public int LocationId { get; set; }
}

namespace Artskart3.Core.Domain.Enums;

/// <summary>
/// Geometritypen til en lokasjon, materialisert i Location.GeometryTypeId.
///
/// HVORFOR EN KOLONNE OG IKKE STGeometryType()
/// Funksjonen kan ikke pushes ned i en indeks og må kalles per rad. Målt på
/// Oslo-utsnittet gikk utsnittssøket fra 175 ms til 951 fordi den ble evaluert
/// på alle 967 649 lokasjonene for å finne de 10 902 med polygon. Med kolonnen:
/// 37 ms, og hele polygonspørringen 1010 ms → 92 ms.
///
/// Verdiene er lagret i databasen. Endre aldri et tall som er tatt i bruk —
/// legg nye typer til på slutten.
/// </summary>
public enum LocationGeometryType : byte
{
    /// <summary>Ukjent eller ikke-kartlagt type. Aldri filtrer PÅ denne.</summary>
    Ukjent = 0,

    /// <summary>Punkt. 5 024 795 av 5 100 794 lokasjoner (98,5 %).</summary>
    Point = 1,

    Polygon = 2,
    MultiPolygon = 3,

    /// <summary>
    /// Transekter, elvestrekninger, kystlinjer. 2 101 lokasjoner med 62 764
    /// observasjoner. Vises IKKE i dag — se GetLocationPolygonsAsync.
    /// </summary>
    LineString = 4,

    MultiLineString = 5,

    /// <summary>Ni lokasjoner. Kan inneholde hva som helst, så de utelates.</summary>
    GeometryCollection = 6,
}

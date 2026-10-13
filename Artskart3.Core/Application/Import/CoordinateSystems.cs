namespace Artskart3.Core.Application.Import;

/// <summary>
/// EPSG-kodene importen forholder seg til. Port av Artskart 2s
/// <c>CoordinateSystem</c>-enum og de to statiske metodene på
/// <c>UtmCoordinate</c>.
///
/// Konstanter og ikke en enum: kodene brukes som tall mot
/// <c>ICoordinateTransformer</c>, mot NetTopologySuite og mot SQL Server, og en enum
/// hadde bare krevd en cast hver gang.
/// </summary>
public static class CoordinateSystems
{
    /// <summary>WGS 84, grader. Det Darwin Core-feltene oppgis i når ingenting annet står.</summary>
    public const int Wgs84LatLon = 4326;

    /// <summary>ED50, grader. <c>GeodeticDatum = "ed50"</c> i kilden.</summary>
    public const int Ed50LatLon = 4230;

    /// <summary>ETRS89, grader. <c>GeodeticDatum = "euref89"</c> i kilden.</summary>
    public const int Etrs89LatLon = 4258;

    /// <summary>WGS 84 / UTM 33N. Det Artskart 2 merket lagrede geometrier med.</summary>
    public const int Wgs84UtmZone33N = 32633;

    /// <summary>
    /// ETRS89 / UTM 33N. Lagringssystemet i Artskart 3 — alt importen oppretter
    /// merkes med denne, jf. migrasjonen <c>RelabelGeometrySridTo25833</c>.
    /// </summary>
    public const int Etrs89UtmZone33N = 25833;

    /// <summary>
    /// EPSG-koden for ETRS89 / UTM i en gitt sone. Sone 30–37 er de eneste gyldige —
    /// samme grense som i Artskart 2, og den dekker alt fra Nordsjøen til Novaja
    /// Semlja.
    /// </summary>
    public static int Etrs89UtmFromZone(int zone)
    {
        if (zone is < 30 or > 37)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), zone, "Bare UTM-sone 30-37 er støttet.");
        }

        return 25800 + zone;
    }

    /// <summary>Sonenummeret bak en ETRS89 / UTM-kode.</summary>
    public static short ZoneFromEtrs89Utm(int epsgCode)
    {
        if (epsgCode is < 25800 or > 25837)
        {
            throw new ArgumentOutOfRangeException(nameof(epsgCode), epsgCode, "Ikke en ETRS89 / UTM-kode.");
        }

        return (short)(epsgCode - 25800);
    }
}

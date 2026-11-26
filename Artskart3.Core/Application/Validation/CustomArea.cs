using Artskart3.Core.Constants;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Artskart3.Core.Application.Validation;

/// <summary>
/// Det egendefinerte området i filteret. Validatoren og oppslaget leser det på samme
/// måte, så et filter som slipper gjennom valideringen alltid kan slås opp.
/// </summary>
public static class CustomArea
{
    // Rikelig for en koordinat med desimaler. Avviser enorme tekster før de leses.
    private const int MaxCharactersPerVertex = 64;

    /// <summary>
    /// Polygonet, eller null når teksten ikke er ett polygon innenfor grensene.
    /// Selvkryssende polygoner godtas; oppslaget reparerer dem.
    /// </summary>
    public static Polygon? Parse(string wkt)
    {
        if (wkt.Length > SearchConstants.MaxCustomAreaVertices * MaxCharactersPerVertex) return null;

        Geometry geometry;
        try
        {
            geometry = new WKTReader().Read(wkt);
        }
        catch (Exception ex) when (ex is ParseException or ArgumentException)
        {
            // ArgumentException kommer fra ringer som ikke er lukket eller har for få punkter.
            return null;
        }

        if (geometry is not Polygon { IsEmpty: false } polygon) return null;
        // NTS godtar en lukket ring med tre punkter, altså to hjørner.
        if (polygon.ExteriorRing.NumPoints < 4) return null;
        if (polygon.NumPoints > SearchConstants.MaxCustomAreaVertices) return null;
        if (polygon.Coordinates.Any(c => !double.IsFinite(c.X) || !double.IsFinite(c.Y))) return null;

        return polygon;
    }
}

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Regler for hvilke lokasjonsgeometrier som vises på kartet.
///
/// Ligger for seg selv fordi BÅDE databasestien i SearchRepository og
/// minnelageret i PolygonLocationStore må bruke nøyaktig samme regel. To
/// implementasjoner som skal gi samme svar er to implementasjoner som en dag
/// ikke gjør det — og forskjellen ville vist seg som polygoner som dukker opp
/// eller forsvinner avhengig av hvilken sti som svarte.
/// </summary>
public static class LocationGeometryRules
{
    /// <summary>
    /// Sann når WKT-strengen er et rektangel — nøyaktig fem koordinatpar i
    /// ytre ring. Det er rutenettrutene, som ikke er reelle geografiske
    /// utstrekninger og derfor ikke skal tegnes.
    ///
    /// Gjelder BARE POLYGON. Regelen teller koordinatpar etter andre parentes, og
    /// det er ytre ring bare for POLYGON((...)). For MULTILINESTRING((...),(...))
    /// er det den første linja, så en transekt med fem punkter ble lest som en
    /// rutenettrute og forsvant fra kartet. Verken MULTIPOLYGON eller
    /// (MULTI)LINESTRING er rutenettruter, så de skal alltid tegnes.
    /// </summary>
    public static bool IsRectangularPolygon(string? wkt)
    {
        if (string.IsNullOrEmpty(wkt)) return false;

        var geometri = wkt.AsSpan().TrimStart();
        if (!geometri.StartsWith("POLYGON", StringComparison.OrdinalIgnoreCase)) return false;

        // Ytre ring ligger etter ANDRE parentes: POLYGON((...)). Er det bare én,
        // blir ringStart -1 — og IndexOf(')', -1) kaster. Vakten må derfor stå
        // før det andre oppslaget, ikke etter.
        var ringStart = wkt.IndexOf('(', wkt.IndexOf('(') + 1);
        if (ringStart < 0) return false;

        var ringEnd = wkt.IndexOf(')', ringStart);
        if (ringEnd < 0) return false;

        var ring = wkt.AsSpan(ringStart + 1, ringEnd - ringStart - 1);
        var commaCount = 0;
        foreach (var ch in ring)
        {
            if (ch == ',') commaCount++;
        }

        return commaCount == 4;
    }
}

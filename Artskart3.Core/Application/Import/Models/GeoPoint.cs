namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Ett punkt med den EPSG-koden det er oppgitt i. Erstatter Artskart 2s
/// <c>Coordinate</c>-hierarki (<c>LatLonCoordinate</c>, <c>UtmCoordinate</c>,
/// <c>GoogleMercatorCoordinate</c>), som bar på en type per koordinatsystem uten at
/// noen av dem hadde egen oppførsel — forskjellen var alltid bare EPSG-koden.
///
/// <see cref="X"/> er østlig retning: lengdegrad i et geografisk system, easting i et
/// projisert. <see cref="Y"/> er nordlig: breddegrad eller northing. Samme
/// rekkefølge som i Artskart 2, og den rekkefølgen er det transformasjonene forventer.
/// </summary>
public readonly record struct GeoPoint(double X, double Y, int Epsg)
{
    /// <summary>
    /// Nullkoordinat. Artskart 2 behandler (0, 0) som «ingen koordinat» — det er midt
    /// i Atlanterhavet, og i praksis et felt som ikke var fylt ut.
    /// </summary>
    public bool IsNull => IsNullPosition(X, Y);

    public static bool IsNullPosition(double x, double y) =>
        Math.Abs(x) < double.Epsilon && Math.Abs(y) < double.Epsilon;
}

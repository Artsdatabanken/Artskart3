namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Bounding box for spatial filtering in EPSG:25833 (UTM Zone 33N).
/// </summary>
public class EnvelopeDto
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    /// <summary>
    /// Maksimal avstand fra origo, i meter. UTM-koordinater i Norge holder seg godt
    /// innenfor noen millioner, så grensen avviser bare det absurde.
    ///
    /// Den finnes fordi utsnittet smalnes til int før områdeluking:
    /// <c>(int)</c> av en double utenfor int-området er uspesifisert i en unchecked
    /// kontekst og gir int.MinValue på x64. AreaBounds.Expand legger så til en
    /// kilometer, tallet renner over til et stort positivt, Intersects blir falsk,
    /// og HVERT område lukes bort — et tomt kart uten en feilmelding.
    /// </summary>
    public const double MaxMagnitude = 100_000_000;

    public bool IsValid =>
        MinX < MaxX && MinY < MaxY && ErEndeligOgInnenfor(MinX) && ErEndeligOgInnenfor(MinY)
        && ErEndeligOgInnenfor(MaxX) && ErEndeligOgInnenfor(MaxY);

    private static bool ErEndeligOgInnenfor(double v) =>
        double.IsFinite(v) && Math.Abs(v) <= MaxMagnitude;
}

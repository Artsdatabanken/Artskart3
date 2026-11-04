namespace Artskart3.Core.Application.Services;

/// <summary>
/// Akseparallell boks i meter (UTM 33N). Brukes både for kartutsnittet og for
/// utstrekningen til et område.
/// </summary>
/// <remarks>
/// Områdene i Area-tabellen er lagret i EPSG:32633 mens kartutsnittet kommer i
/// 25833. De to er samme projeksjon på ulikt datum og ligger omtrent én meter fra
/// hverandre — uten betydning her, og uansett dekket av marginen som
/// AreaHierarchyService legger på.
/// </remarks>
public readonly record struct AreaBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    /// <summary>
    /// Overlapper de to boksene? Berøring i kanten teller som overlapp.
    /// </summary>
    public bool Intersects(AreaBounds other) =>
        MinX <= other.MaxX && MaxX >= other.MinX &&
        MinY <= other.MaxY && MaxY >= other.MinY;

    /// <summary>
    /// Utvider boksen like mye i alle retninger.
    /// </summary>
    public AreaBounds Expand(int metres) =>
        new(MinX - metres, MinY - metres, MaxX + metres, MaxY + metres);
}

using System.Text.Json;

namespace Artskart3.Core.Application.ExternalModels;

/// <summary>
/// GeoJSON-svaret fra Kartverkets Eiendom API (/geokoding med omrade=true).
/// Hver feature er én teig; en matrikkelenhet kan ha mange.
/// </summary>
public class KartverketFeatureCollection
{
    public List<KartverketFeature> Features { get; set; } = [];
}

public class KartverketFeature
{
    public KartverketGeometry? Geometry { get; set; }
}

public class KartverketGeometry
{
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Polygon: [ring][punkt][x, y]. MultiPolygon: [polygon][ring][punkt][x, y].
    /// </summary>
    public JsonElement Coordinates { get; set; }
}

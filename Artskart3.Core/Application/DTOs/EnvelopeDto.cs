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

    public bool IsValid => MinX < MaxX && MinY < MaxY;
}

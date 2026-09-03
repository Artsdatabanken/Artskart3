namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Represents a location's polygon geometry.
/// </summary>
public class LocationPolygonDto
{
    public int LocationId { get; set; }
    public string? Locality { get; set; }
    public string WktPolygon { get; set; } = null!;
    public int ObservationCount { get; set; }
}

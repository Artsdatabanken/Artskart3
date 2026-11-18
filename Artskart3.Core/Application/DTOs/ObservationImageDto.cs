namespace Artskart3.Core.Application.DTOs;

public class ObservationImageDto
{
    public int ObservationId { get; init; }
    public string? PopularName { get; init; }
    public string? ScientificName { get; init; }
    public string? Author { get; init; }
    public ObservationPointDto? Point { get; set; }
    public int? CoordinatePrecision { get; init; }
    public ObservationMediaDto Image { get; init; } = new();
}

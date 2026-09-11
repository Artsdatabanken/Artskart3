namespace Artskart3.Core.Application.DTOs;

public class CentroidDto
{
    public required double X { get; set; }
    public required double Y { get; set; }
}

public class AreaMarkerDto
{
    public required int Id { get; set; }
    public required string DocumentId { get; set; }
    public required string Fid { get; set; }
    public required string Name { get; set; }
    public required int AreaTypeId { get; set; }
    public required string ParentFid { get; set; }
    public required int ObservationCount { get; set; }
    public required string? WktsPolygon { get; set; }
    // Ikke "required": Swashbuckle emitter ellers nullable referansetyper som non-nullable i OpenAPI-skjemaet
    public CentroidDto? Centroid { get; set; }
}

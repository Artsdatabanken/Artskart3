namespace Artskart3.Core.Application.DTOs;

public class ObservationDetailDto
{
    public int Id { get; init; }
    public string? PopularName { get; init; }
    public string? ScientificName { get; init; }
    public int ExternalTaxonId { get; init; }
    public string? CategoryCode { get; init; }
    public int? CategoryTypeId { get; init; }
    public string? AssessmentUrl { get; set; }
    public DateTime? Collected { get; init; }
    public string? Collector { get; init; }
    public string? BasisOfRecord { get; init; }
    public List<string> Behaviors { get; init; } = [];
    public int? Quality { get; init; }
    public List<string> Tags { get; init; } = [];
    public bool HasErrors { get; init; }
    public string? Locality { get; init; }
    public List<string> Counties { get; init; } = [];
    public List<string> Municipalities { get; init; } = [];
    public ObservationPointDto? Point { get; set; }
    public int? CoordinatePrecision { get; init; }
    public string? Institution { get; init; }
    public string? Dataset { get; init; }
    public List<string> Projects { get; set; } = [];
    public string? CatalogNumber { get; init; }
    public List<ObservationMediaDto> Images { get; set; } = [];
}

public record ObservationPointDto(double Latitude, double Longitude, int East, int North);

public class ObservationMediaDto
{
    public int Id { get; init; }
    public string? Origin { get; set; }
    public string MimeType { get; init; } = "";
    public bool HasStoredImage { get; init; }
    public string? Description { get; init; }
    public string? RightsHolder { get; init; }
    public string? License { get; init; }
}

public record ObservationImageFile(byte[] Bytes, string ContentType);

namespace Artskart3.Core.Application.DTOs;

public class MapLayerDto
{
    public required int Id { get; set; }
    public required string Name { get; set; } = null!;
    public required string Type { get; set; } = null!;
    public required string Url { get; set; } = null!;
    public string? Layers { get; set; }
    public string? Format { get; set; }
    public string? Version { get; set; }
    public string? Attribution { get; set; }
    public double? MinScaleDenominator { get; set; }
    public double? MaxScaleDenominator { get; set; }
    public required bool UseSingleImage { get; set; }
    public required int RetryAfterSeconds { get; set; }
    public required double Opacity { get; set; }
}

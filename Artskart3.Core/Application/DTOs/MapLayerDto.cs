namespace Artskart3.Core.Application.DTOs;

public class MapLayerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string? Layers { get; set; }
    public string? Format { get; set; }
    public string? Version { get; set; }
    public string? Attribution { get; set; }
}

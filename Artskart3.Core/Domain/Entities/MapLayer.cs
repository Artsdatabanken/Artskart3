using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public class MapLayer : BaseEntity
{
    public string Name { get; set; } = null!;

    /// <summary>
    /// WMS, WMTS or Tile
    /// </summary>
    public string Type { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? Layers { get; set; }

    public string? Format { get; set; }

    public string? Version { get; set; }

    public string? Attribution { get; set; }
}

namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Normalisert stedsnavntreff returnert av Artskart3-backend etter søk mot Geonorge.
/// Koordinaten er alltid i kartets projeksjon (EPSG:25833), se <see cref="Configuration.GeonorgeOptions.OutputCoordinateSystem"/>.
/// </summary>
public class PlaceSearchResultDto
{
    public int StedsNummer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NavneObjektType { get; set; } = string.Empty;

    /// <summary>
    /// Anbefalt kartzoom-nivå basert på stedsnavntypen, se <see cref="Artskart3.Core.Application.ExternalModels.NavneTyper"/>.
    /// </summary>
    public int RecommendedZoom { get; set; }
    public double East { get; set; }
    public double North { get; set; }
    public int CoordinateSystem { get; set; }
    public List<string> Municipalities { get; set; } = [];
    public List<string> Counties { get; set; } = [];
    public List<PlaceNameAlternativeDto> AlternativeNames { get; set; } = [];
}

public class PlaceNameAlternativeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Language { get; set; }
}

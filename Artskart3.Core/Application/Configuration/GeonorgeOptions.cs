namespace Artskart3.Core.Application.Configuration;

public class GeonorgeOptions
{
    public const string SectionName = "Geonorge";

    public string BaseUrl { get; set; } = "https://ws.geonorge.no/stedsnavn/v1/";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Koordinatsystem (SRID) Geonorge skal returnere geometrien i. 25833 tilsvarer kartets
    /// projeksjon (EPSG:25833), slik at klienten ikke selv trenger å reprojisere resultatene.
    /// </summary>
    public int OutputCoordinateSystem { get; set; } = 25833;
}

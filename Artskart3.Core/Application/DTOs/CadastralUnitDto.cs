namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// En matrikkelenhet (gårds- og bruksnummer) med omrisset fra Kartverket.
/// </summary>
public class CadastralUnitDto
{
    /// <summary>
    /// Matrikkelnummeret, f.eks. 1101-1/2. Brukes som ID i filteret.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Alle teigene som én MULTIPOLYGON i EPSG:25833.
    /// </summary>
    public string WktPolygon { get; set; } = null!;

    /// <summary>
    /// Omsluttende rektangel for alle teigene, i EPSG:25833.
    /// </summary>
    public MapExtentDto Extent { get; set; } = null!;
}

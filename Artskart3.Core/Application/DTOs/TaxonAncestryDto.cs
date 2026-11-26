namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Foreldrekjeden og navnene til et taxon.
/// Brukes av frontend til å avgjøre indeterminate/checked-tilstand i taxon-treet
/// og til å vise navnene på valgte taxa som ikke nødvendigvis er lastet i treet ennå.
/// </summary>
public class TaxonAncestryDto
{
    public int Id { get; set; }

    /// <summary>
    /// Foreldrekjeden, sortert fra rotnivå til nærmeste forelder.
    /// Tom for rotnoder og ukjente taxonId-er.
    /// </summary>
    public List<int> ParentIds { get; set; } = [];

    /// <summary>Null for ukjente taxonId-er.</summary>
    public string? ValidScientificName { get; set; }

    public string? PreferredPopularName { get; set; }
}

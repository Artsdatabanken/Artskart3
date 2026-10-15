namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Foreldrekjeden for et taxon.
/// Brukes av frontend til å avgjøre indeterminate/checked-tilstand i taxon-treet
/// for taxa som ikke nødvendigvis er lastet i treet ennå.
/// </summary>
public class TaxonAncestryDto
{
    public int Id { get; set; }

    /// <summary>
    /// Foreldrekjeden, sortert fra rotnivå til nærmeste forelder.
    /// Tom for rotnoder og ukjente taxonId-er.
    /// </summary>
    public List<int> ParentIds { get; set; } = [];
}

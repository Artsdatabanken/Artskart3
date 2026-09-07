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

    /// <summary>
    /// Synlige barn per nivå i kjeden (alle forgjengere samt taxonet selv).
    /// Lar frontend avgjøre om en forgjenger er fullt dekket av utvalget uten
    /// at treet er ekspandert ned til nivået.
    /// </summary>
    public List<TaxonAncestryLevelDto> Levels { get; set; } = [];
}

/// <summary>
/// Synlige barn av én node i en foreldrekjede.
/// </summary>
public class TaxonAncestryLevelDto
{
    public int ParentId { get; set; }

    /// <summary>
    /// Barn filtrert på samme måte som TaxonTree (kun taxoner med observasjoner
    /// eller som finnes i landet).
    /// </summary>
    public List<int> ChildIds { get; set; } = [];
}

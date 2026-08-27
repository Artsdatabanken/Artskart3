namespace Artskart3.Core.Application.DTOs;

public class TaxonTreeNodeDto
{
    public int Id { get; set; }
    public string? ValidScientificName { get; set; }
    public string? PreferredPopularName { get; set; }
    public int TaxonRankId { get; set; }
    public int TaxonGroupId { get; set; }
    public int? CumulativeObservationCount { get; set; }
    public bool ExistsInCountry { get; set; }
    public bool HasChildren { get; set; }

    /// <summary>
    /// Hele forelderkjeden for noden, sortert fra rotnivå til nærmeste forelder.
    /// Tom liste for rotnoder. Nodene her har alltid tomme Parents- og Children-lister.
    /// </summary>
    public List<TaxonTreeNodeDto> Parents { get; set; } = [];

    public List<TaxonTreeNodeDto> Children { get; set; } = [];
}

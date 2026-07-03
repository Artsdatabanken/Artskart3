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
    public List<TaxonTreeNodeDto> Children { get; set; } = [];
}

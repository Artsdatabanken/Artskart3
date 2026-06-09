namespace Artskart3.Core.Application.DTOs
{
    public class TaxonDto
    {
        public int Id { get; set; }
        public int ExternalTaxonId { get; set; }
        public string? ValidScientificName { get; set; }
        public string? ValidScientificNameAuthorship { get; set; }
        public string? PreferredPopularName { get; set; }
        public int TaxonGroupId { get; set; }
        public int TaxonRankId { get; set; }
        public int? CumulativeObservationCount { get; set; }
        public bool ExistsInCountry { get; set; }
    }
}

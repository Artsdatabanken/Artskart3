namespace Artskart3.Core.Application.DTOs
{
    public class ObservationSearchFilterDto : PaginatedRequestDto
    {
        public string? PreferredPopularName { get; set; }

        public string? ScientificName { get; set; }

        public string? Author { get; set; }

        public int[]? TaxonGroupIds { get; set; }

        public int[]? RisikokategoriIder { get; set; }

        public int[]? OrganizationIds { get; set; }

        public string? Locality { get; set; }

        public string[]? MunicipalityIds { get; set; }
    }
}

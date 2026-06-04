namespace Artskart3.Core.Application.DTOs
{
    public class ObservationSearchFilterDto : PaginatedRequestDto
    {
        public string? PreferredPopularName { get; set; }

        public string? ScientificName { get; set; }

        public string? Author { get; set; }

        public int[]? TaxonGroupIds { get; set; }

        public int[]? CategoryIds { get; set; }

        public int[]? OrganizationIds { get; set; }

        public string[]? MunicipalityIds { get; set; }

        public string[]? CountyIds { get; set; }

        public int[]? BehaviorIds { get; set; }

        public int[]? BasisOfRecordIds { get; set; }

        public CoordinatePrecisionDto? CoordinatePrecision { get; set; }

        public PeriodDto? Period { get; set; }
    }
}

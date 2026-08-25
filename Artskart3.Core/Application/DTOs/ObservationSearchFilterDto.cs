namespace Artskart3.Core.Application.DTOs;

public class ObservationSearchFilterDto : PaginatedRequestDto, IObservationFilter
{
    public string? PreferredPopularName { get; set; }

    public string? ScientificName { get; set; }

    public string? Author { get; set; }

    public int[]? TaxonGroupIds { get; set; }

    public int[]? CategoryIds { get; set; }

    public int[]? OrganizationIds { get; set; }

    public string[]? MunicipalityIds { get; set; }

    public string[]? CountyIds { get; set; }

    public string[]? RestrictedAreaIds { get; set; }

    public string[]? OceanAreaIds { get; set; }

    public int[]? BehaviorIds { get; set; }

    public int[]? BasisOfRecordIds { get; set; }

    public int? RegistrationStatusId { get; set; }

    public CoordinatePrecisionDto? CoordinatePrecision { get; set; }

    public PeriodDto? Period { get; set; }

    public string? ProjectName { get; set; }

    public int? ProjectOrganizationId { get; set; }

    public string? CollectionCode { get; set; }

    public string? CatalogNumber { get; set; }

    public bool? WithImages { get; set; }
}

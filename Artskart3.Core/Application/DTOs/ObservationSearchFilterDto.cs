using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.DTOs;

public class ObservationSearchFilterDto : PaginatedRequestDto, IObservationFilter
{
    public int[]? TaxonGroupIds { get; set; }

    public int[]? TaxonIds { get; set; }

    public bool IncludeDescendantTaxa { get; set; } = true;

    public int[]? CategoryIds { get; set; }

    public int[]? OrganizationIds { get; set; }

    public string[]? MunicipalityIds { get; set; }

    public string[]? CountyIds { get; set; }

    public string[]? RestrictedAreaIds { get; set; }

    public string[]? OceanAreaIds { get; set; }

    public string[]? CadastralUnitIds { get; set; }

    public int[]? BehaviorIds { get; set; }

    public int[]? BasisOfRecordIds { get; set; }

    public int? RegistrationStatusId { get; set; }

    public CoordinatePrecisionDto? CoordinatePrecision { get; set; }

    public PeriodDto? Period { get; set; }

    public int[]? DatasetOrgIds { get; set; }

    public int[]? ProjectOrgIds { get; set; }

    public int[]? ObservationIds { get; set; }

    public bool? WithImages { get; set; }

    public bool? ChangedByAnnotation { get; set; }

    public HiddenObservationFilter HiddenObservations { get; set; }

    public bool? Sensitive { get; set; }

    public bool? UncertainIdentification { get; set; }

    public bool? Introduced { get; set; }

    /// <summary>
    /// Samme filter, begrenset til funn med mediefiler.
    /// </summary>
    public ObservationSearchFilterDto WithMediaFilesOnly()
    {
        var copy = (ObservationSearchFilterDto)MemberwiseClone();
        copy.WithImages = true;
        return copy;
    }
}

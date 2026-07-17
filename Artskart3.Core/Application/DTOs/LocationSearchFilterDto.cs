namespace Artskart3.Core.Application.DTOs;

public class LocationSearchFilterDto : IObservationFilter
{
    public int[]? TaxonGroupIds { get; set; }

    public int[]? CategoryIds { get; set; }

    public int[]? BasisOfRecordIds { get; set; }

    public int[]? OrganizationIds { get; set; }

    public string[]? MunicipalityIds { get; set; }

    public string[]? CountyIds { get; set; }

    public string[]? RestrictedAreaIds { get; set; }

    public string[]? OceanAreaIds { get; set; }

    public int[]? BehaviorIds { get; set; }

    public int? RegistrationStatusId { get; set; }

    public CoordinatePrecisionDto? CoordinatePrecision { get; set; }

    public PeriodDto? Period { get; set; }

    /// <summary>
    /// Returnerer true dersom minst ett søkefilter er satt (ekskluderer Envelope, Epsg, MaxResults).
    /// </summary>
    public bool HasActiveFilters =>
        TaxonGroupIds?.Length > 0 ||
        CategoryIds?.Length > 0 ||
        BasisOfRecordIds?.Length > 0 ||
        RegistrationStatusId.HasValue ||
        OrganizationIds?.Length > 0 ||
        MunicipalityIds?.Length > 0 ||
        CountyIds?.Length > 0 ||
        RestrictedAreaIds?.Length > 0 ||
        OceanAreaIds?.Length > 0 ||
        BehaviorIds?.Length > 0 ||
        CoordinatePrecision?.From != null ||
        CoordinatePrecision?.To != null ||
        Period?.From != null ||
        Period?.To != null;

    /// <summary>
    /// Returnerer true dersom det finnes filtre som krever dynamisk telling av observasjoner.
    /// Inkluderer observasjonsattributter (takson, kategori, atferd, etc.) og verneområder
    /// (som krysser fylkes-/kommunegrenser). Fylker, kommuner og havområder bruker
    /// pre-beregnede antall og filtrerer kun hvilke områdemarkører som vises.
    /// </summary>
    public bool HasObservationAttributeFilters =>
        TaxonGroupIds?.Length > 0 ||
        CategoryIds?.Length > 0 ||
        BasisOfRecordIds?.Length > 0 ||
        RegistrationStatusId.HasValue ||
        OrganizationIds?.Length > 0 ||
        RestrictedAreaIds?.Length > 0 ||
        BehaviorIds?.Length > 0 ||
        CoordinatePrecision?.From != null ||
        CoordinatePrecision?.To != null ||
        Period?.From != null ||
        Period?.To != null;

    /// <summary>
    /// EPSG code for coordinate system (default: 25833)
    /// </summary>
    public int? Epsg { get; set; }

    /// <summary>
    /// Maximum number of locations to return (default: 100000, max: 100000)
    /// </summary>
    public int MaxResults { get; set; } = 100000;

    /// <summary>
    /// Kartutsnitt (bounding box) for spatial filtrering i EPSG:25833 (UTM Zone 33N).
    /// </summary>
    public EnvelopeDto? Envelope { get; set; }
}

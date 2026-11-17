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

    public int[]? HistoricalAreaIds { get; set; }

    public string? CustomAreaWkt { get; set; }

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

    /// <summary>
    /// Samme filter med det egendefinerte området uttrykt som områdene helt innenfor det og
    /// en lokasjonsliste for kanten, som behandles som en matrikkelenhet. Bare for filtre uten
    /// andre områdevalg, siden områdene og listen OR-es.
    /// </summary>
    public ObservationSearchFilterDto WithCustomAreaAsAreas(string[] municipalityIds, string[] oceanAreaIds, string[] countyIds, string edgeLocationsKey)
    {
        var copy = (ObservationSearchFilterDto)MemberwiseClone();
        copy.MunicipalityIds = municipalityIds.Length > 0 ? municipalityIds : null;
        copy.OceanAreaIds = oceanAreaIds.Length > 0 ? oceanAreaIds : null;
        // Svalbard, Bjørnøya og Jan Mayen slås opp med fylkes-ID-ene, som ellers i løsningen.
        copy.CountyIds = countyIds.Length > 0 ? countyIds : null;
        copy.CadastralUnitIds = [edgeLocationsKey];
        copy.CustomAreaWkt = null;
        return copy;
    }
}

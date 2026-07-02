namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Felles filteregenskaper delt mellom Location- og Observation-søk.
/// Brukes av ApplyCommonFilters i SearchRepository for å unngå duplisert filterlogikk.
/// </summary>
public interface IObservationFilter
{
    int[]? TaxonGroupIds { get; }
    int[]? CategoryIds { get; }
    int[]? OrganizationIds { get; }
    string[]? MunicipalityIds { get; }
    string[]? CountyIds { get; }
    string[]? RestrictedAreaIds { get; }
    string[]? OceanAreaIds { get; }
    int[]? BehaviorIds { get; }
    int[]? BasisOfRecordIds { get; }
    CoordinatePrecisionDto? CoordinatePrecision { get; }
    PeriodDto? Period { get; }
}

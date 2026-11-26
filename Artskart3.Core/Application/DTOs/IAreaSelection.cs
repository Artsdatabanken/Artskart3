namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Områdevalgene i et søkefilter, med settere, så begge filtertypene deler oppdelingen av det
/// egendefinerte området.
/// </summary>
internal interface IAreaSelection
{
    string[]? MunicipalityIds { get; set; }
    string[]? CountyIds { get; set; }
    string[]? OceanAreaIds { get; set; }
    string[]? CadastralUnitIds { get; set; }
    string? CustomAreaWkt { get; set; }

    /// <summary>
    /// Bytter ut det egendefinerte området i <paramref name="filter"/> med områdene helt innenfor
    /// det og en lokasjonsliste for kanten, som behandles som en matrikkelenhet. Bare for filtre
    /// uten andre områdevalg, se <see cref="Validation.ObservationFilterValidator.CanSplitCustomArea"/>.
    /// </summary>
    static T ReplaceCustomAreaWithAreas<T>(
        T filter, string[] municipalityIds, string[] oceanAreaIds, string[] countyIds, string edgeLocationsKey)
        where T : IAreaSelection
    {
        filter.MunicipalityIds = municipalityIds.Length > 0 ? municipalityIds : null;
        filter.OceanAreaIds = oceanAreaIds.Length > 0 ? oceanAreaIds : null;
        // Svalbard, Bjørnøya og Jan Mayen slås opp med fylkes-ID-ene, som ellers i løsningen.
        filter.CountyIds = countyIds.Length > 0 ? countyIds : null;
        filter.CadastralUnitIds = [edgeLocationsKey];
        filter.CustomAreaWkt = null;
        return filter;
    }
}

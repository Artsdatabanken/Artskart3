using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.DTOs;

public class LocationSearchFilterDto : IObservationFilter
{
    public int[]? TaxonGroupIds { get; set; }

    public int[]? TaxonIds { get; set; }

    public bool IncludeDescendantTaxa { get; set; } = true;

    public int[]? CategoryIds { get; set; }

    public int[]? BasisOfRecordIds { get; set; }

    public int[]? OrganizationIds { get; set; }

    public string[]? MunicipalityIds { get; set; }

    public string[]? CountyIds { get; set; }

    public string[]? RestrictedAreaIds { get; set; }

    public string[]? OceanAreaIds { get; set; }

    public string[]? CadastralUnitIds { get; set; }

    public int[]? HistoricalAreaIds { get; set; }

    public string? CustomAreaWkt { get; set; }

    public int[]? BehaviorIds { get; set; }

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
    /// Returnerer true dersom minst ett søkefilter er satt (ekskluderer Envelope, Epsg, MaxResults).
    /// </summary>
    public bool HasActiveFilters =>
        TaxonGroupIds?.Length > 0 ||
        TaxonIds?.Length > 0 ||
        CategoryIds?.Length > 0 ||
        BasisOfRecordIds?.Length > 0 ||
        RegistrationStatusId.HasValue ||
        OrganizationIds?.Length > 0 ||
        MunicipalityIds?.Length > 0 ||
        CountyIds?.Length > 0 ||
        RestrictedAreaIds?.Length > 0 ||
        OceanAreaIds?.Length > 0 ||
        CadastralUnitIds?.Length > 0 ||
        HistoricalAreaIds?.Length > 0 ||
        CustomAreaWkt != null ||
        BehaviorIds?.Length > 0 ||
        CoordinatePrecision?.From != null ||
        CoordinatePrecision?.To != null ||
        Period?.From != null ||
        Period?.To != null ||
        Period?.Months?.Length > 0 ||
        DatasetOrgIds?.Length > 0 ||
        ProjectOrgIds?.Length > 0 ||
        ObservationIds?.Length > 0 ||
        WithImages != null ||
        ChangedByAnnotation != null ||
        HiddenObservations != HiddenObservationFilter.Exclude ||
        Sensitive != null ||
        UncertainIdentification != null ||
        Introduced != null;

    /// <summary>
    /// Returnerer true dersom det finnes filtre som krever dynamisk telling av observasjoner.
    /// Inkluderer observasjonsattributter (takson, kategori, atferd, etc.), verneområder
    /// (som krysser fylkes-/kommunegrenser), matrikkelenheter, historiske områder og
    /// egendefinerte områder (som ikke er forhåndstalt). Gjeldende fylker, kommuner og
    /// havområder bruker pre-beregnede antall og filtrerer kun hvilke områdemarkører som vises.
    /// </summary>
    public bool HasObservationAttributeFilters =>
        TaxonGroupIds?.Length > 0 ||
        TaxonIds?.Length > 0 ||
        CategoryIds?.Length > 0 ||
        BasisOfRecordIds?.Length > 0 ||
        RegistrationStatusId.HasValue ||
        OrganizationIds?.Length > 0 ||
        RestrictedAreaIds?.Length > 0 ||
        CadastralUnitIds?.Length > 0 ||
        HistoricalAreaIds?.Length > 0 ||
        CustomAreaWkt != null ||
        BehaviorIds?.Length > 0 ||
        CoordinatePrecision?.From != null ||
        CoordinatePrecision?.To != null ||
        Period?.From != null ||
        Period?.To != null ||
        Period?.Months?.Length > 0 ||
        DatasetOrgIds?.Length > 0 ||
        ProjectOrgIds?.Length > 0 ||
        ObservationIds?.Length > 0 ||
        WithImages != null ||
        ChangedByAnnotation != null ||
        HiddenObservations != HiddenObservationFilter.Exclude ||
        Sensitive != null ||
        UncertainIdentification != null ||
        Introduced != null;

    /// <summary>
    /// EPSG code for coordinate system (default: 25833)
    /// </summary>
    public int? Epsg { get; set; }

    /// <summary>
    /// Maximum number of locations to return (default: 100000, max: 200000)
    /// </summary>
    public int MaxResults { get; set; } = 100000;

    /// <summary>
    /// Kartutsnitt (bounding box) for spatial filtrering i EPSG:25833 (UTM Zone 33N).
    /// </summary>
    public EnvelopeDto? Envelope { get; set; }

    /// <summary>
    /// Om filteret har områdevalg som slås opp via lokasjonene: matrikkelenheter eller
    /// historiske områder.
    /// </summary>
    public bool HasLocationBasedAreas => CadastralUnitIds?.Length > 0 || HistoricalAreaIds?.Length > 0;

    /// <summary>
    /// Samme filter uten matrikkelenhetene og de historiske områdene.
    /// </summary>
    public LocationSearchFilterDto WithoutLocationBasedAreas()
    {
        var copy = (LocationSearchFilterDto)MemberwiseClone();
        copy.CadastralUnitIds = null;
        copy.HistoricalAreaIds = null;
        return copy;
    }

    /// <summary>
    /// Om filteret har lokasjonslister fra matrikkelenheter eller et egendefinert område.
    /// </summary>
    public bool HasLocationLists => CadastralUnitIds?.Length > 0 || CustomAreaWkt != null;

    /// <summary>
    /// Samme filter uten matrikkelenhetene, de historiske områdene og det egendefinerte området.
    /// </summary>
    public LocationSearchFilterDto WithoutLocationLists()
    {
        var copy = WithoutLocationBasedAreas();
        copy.CustomAreaWkt = null;
        return copy;
    }

    /// <summary>
    /// Samme filter med det egendefinerte området uttrykt som områdene helt innenfor det og
    /// en lokasjonsliste for kanten, som behandles som en matrikkelenhet. Bare for filtre uten
    /// andre områdevalg, siden områdene og listen OR-es.
    /// </summary>
    public LocationSearchFilterDto WithCustomAreaAsAreas(string[] municipalityIds, string[] oceanAreaIds, string[] countyIds, string edgeLocationsKey)
    {
        var copy = (LocationSearchFilterDto)MemberwiseClone();
        copy.MunicipalityIds = municipalityIds.Length > 0 ? municipalityIds : null;
        copy.OceanAreaIds = oceanAreaIds.Length > 0 ? oceanAreaIds : null;
        // Svalbard, Bjørnøya og Jan Mayen slås opp med fylkes-ID-ene, som ellers i løsningen.
        copy.CountyIds = countyIds.Length > 0 ? countyIds : null;
        copy.CadastralUnitIds = [edgeLocationsKey];
        copy.CustomAreaWkt = null;
        return copy;
    }
}

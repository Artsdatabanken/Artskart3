namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Felles filteregenskaper delt mellom Location- og Observation-søk.
/// Brukes av ApplyCommonFilters i SearchRepository for å unngå duplisert filterlogikk.
/// </summary>
public interface IObservationFilter
{
    int[]? TaxonGroupIds { get; }
    int[]? TaxonIds { get; }

    /// <summary>
    /// Om TaxonIds også treffer underliggende taksa. Av: bare observasjoner bestemt
    /// til nøyaktig det valgte taksonet.
    /// </summary>
    bool IncludeDescendantTaxa { get; }

    int[]? CategoryIds { get; }
    int[]? OrganizationIds { get; }
    string[]? MunicipalityIds { get; }
    string[]? CountyIds { get; }
    string[]? RestrictedAreaIds { get; }
    string[]? OceanAreaIds { get; }

    /// <summary>
    /// Matrikkelnummer (f.eks. 1101-1/2). OR-es med de andre områdefiltrene.
    /// Slås opp mot Kartverket og blir til lokasjoner via ICadastralUnitService.
    /// </summary>
    string[]? CadastralUnitIds { get; }

    int[]? BehaviorIds { get; }
    int[]? BasisOfRecordIds { get; }
    int? RegistrationStatusId { get; }
    CoordinatePrecisionDto? CoordinatePrecision { get; }
    PeriodDto? Period { get; }
    /// <summary>
    /// Datasett — Organization med OrganizationTypeId = 2. Velges i typeahead.
    /// </summary>
    int[]? DatasetOrgIds { get; }

    /// <summary>
    /// Prosjekt — Organization med OrganizationTypeId = 3. Velges i typeahead.
    /// </summary>
    int[]? ProjectOrgIds { get; }

    /// <summary>
    /// Observasjoner valgt direkte, i praksis fra katalognummer-typeaheaden.
    ///
    /// Katalognummer er tilnærmet unikt (54,7M distinkte verdier over 61M
    /// observasjoner), så oppslaget skjer i endepunktet og filteret får IDer.
    /// Filterspørringen slipper dermed strengsammenligning helt — det var
    /// `LIKE '%x%'` mot 61M rader som gjorde dette filteret 18-21 sekunder.
    /// Verste fanout på én verdi er 675 observasjoner, så listen er alltid kort.
    /// </summary>
    int[]? ObservationIds { get; }

    bool? WithImages { get; }

    /// <summary>
    /// Taggen AnnotatedChanges, ikke HasAnnotations-kolonnen. Kolonnen tar også med
    /// observasjoner som har en annotering uten at noe ble endret.
    /// </summary>
    bool? ChangedByAnnotation { get; }

    /// <summary>
    /// Taggen Sensitive. Søket skal aldri røre SensitiveObservationData.
    /// </summary>
    bool? Sensitive { get; }
}

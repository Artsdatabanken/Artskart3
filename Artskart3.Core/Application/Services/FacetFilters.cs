using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// Filtervarianter fasett-tellingene trenger.
/// </summary>
public static class FacetFilters
{
    /// <summary>
    /// Filteret slik områdefasetten skal telles med: alt brukeren har valgt, MINUS
    /// geografien og minus kartutsnittet.
    ///
    /// GEOGRAFIEN FJERNES fordi det er fasettens egen dimensjon. Står den igjen, viser
    /// det valgte fylket sitt tall og alle andre null — og da er tallet ubrukelig til
    /// det det er til for, nemlig å vise hva et annet valg ville gitt.
    ///
    /// UTSNITTET FJERNES fordi fasett-tallene følger filteret, ikke hva som er synlig
    /// på skjermen. Uten dette ville tallene i filterpanelet endret seg når brukeren
    /// panorerer kartet. Det er også forutsetningen for at bufferen kan svare.
    ///
    /// MATRIKKELENHETER BEHOLDES. De er ikke en områdedimensjon i bufferen, men et
    /// filter på hvilke observasjoner som teller med — og brukeren har valgt dem.
    ///
    /// NYE FILTERFELT MÅ LEGGES TIL HER. <c>FacetFiltersTests</c> speiler
    /// <see cref="IObservationFilter"/> og feiler hvis et felt mangler, nettopp fordi
    /// et glemt felt gir for høye tall uten at noe annet merker det.
    /// </summary>
    public static LocationSearchFilterDto ForAreaFacet(IObservationFilter filter) =>
        new()
        {
            TaxonGroupIds = filter.TaxonGroupIds,
            TaxonIds = filter.TaxonIds,
            IncludeDescendantTaxa = filter.IncludeDescendantTaxa,
            CategoryIds = filter.CategoryIds,
            OrganizationIds = filter.OrganizationIds,
            BehaviorIds = filter.BehaviorIds,
            BasisOfRecordIds = filter.BasisOfRecordIds,
            RegistrationStatusId = filter.RegistrationStatusId,
            CoordinatePrecision = filter.CoordinatePrecision,
            Period = filter.Period,
            DatasetOrgIds = filter.DatasetOrgIds,
            ProjectOrgIds = filter.ProjectOrgIds,
            ObservationIds = filter.ObservationIds,
            CadastralUnitIds = filter.CadastralUnitIds,
            WithImages = filter.WithImages,
            ChangedByAnnotation = filter.ChangedByAnnotation,
            Sensitive = filter.Sensitive,
            UncertainIdentification = filter.UncertainIdentification,
            Introduced = filter.Introduced,

            // Geografien og utsnittet står med vilje tomme, se oppsummeringen over.
            MunicipalityIds = null,
            CountyIds = null,
            RestrictedAreaIds = null,
            OceanAreaIds = null,
            Envelope = null,
        };

    /// <summary>
    /// Feltene <see cref="ForAreaFacet"/> bevisst IKKE kopierer. Står her slik at
    /// snublesnoren i testene kan skille «glemt» fra «utelatt med vilje».
    /// </summary>
    public static readonly IReadOnlySet<string> DeliberatelyOmitted =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(IObservationFilter.MunicipalityIds),
            nameof(IObservationFilter.CountyIds),
            nameof(IObservationFilter.RestrictedAreaIds),
            nameof(IObservationFilter.OceanAreaIds),
        };
}

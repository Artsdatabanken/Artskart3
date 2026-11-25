using System.Reflection;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Snublesnor for filterkopien områdefasetten bruker.
///
/// <see cref="FacetFilters.ForAreaFacet"/> bygger et nytt filter felt for felt. Et felt
/// som glemmes blir stille borte, og da teller områdefasetten FLERE observasjoner enn
/// filteret egentlig slipper gjennom — riktige tall for feil filter, uten feilmelding.
///
/// Testen speiler <see cref="IObservationFilter"/> med refleksjon, så et nytt felt på
/// grensesnittet feiler her til noen har tatt stilling til det.
/// </summary>
public class FacetFiltersTests
{
    /// <summary>Et filter der hvert felt har en verdi som skiller seg fra default.</summary>
    private static ObservationSearchFilterDto Fullt() => new()
    {
        TaxonGroupIds = [8],
        TaxonIds = [1234],
        IncludeDescendantTaxa = false,
        CategoryIds = [9],
        OrganizationIds = [11],
        MunicipalityIds = ["4601"],
        CountyIds = ["50"],
        RestrictedAreaIds = ["Naturbase VV123"],
        OceanAreaIds = ["22"],
        CadastralUnitIds = ["1101-1/2"],
        BehaviorIds = [3],
        BasisOfRecordIds = [1],
        RegistrationStatusId = 2,
        CoordinatePrecision = new CoordinatePrecisionDto { From = 10, To = 100 },
        Period = new PeriodDto { From = 2020, To = 2024, Months = [6] },
        DatasetOrgIds = [77],
        ProjectOrgIds = [88],
        ObservationIds = [999],
        WithImages = true,
        ChangedByAnnotation = true,
        Sensitive = false,
        UncertainIdentification = true,
        Introduced = false,
    };

    [Fact]
    public void Geografien_fjernes()
    {
        // Fasettens egen dimensjon skal ikke begrense. Står den igjen, viser det valgte
        // fylket sitt tall og alle andre null.
        var resultat = FacetFilters.ForAreaFacet(Fullt());

        resultat.MunicipalityIds.Should().BeNull();
        resultat.CountyIds.Should().BeNull();
        resultat.RestrictedAreaIds.Should().BeNull();
        resultat.OceanAreaIds.Should().BeNull();
    }

    [Fact]
    public void Kartutsnittet_settes_ikke()
    {
        // Fasett-tallene følger filteret, ikke hva som er synlig på skjermen — ellers
        // ville tallene endret seg når brukeren panorerer.
        FacetFilters.ForAreaFacet(Fullt()).Envelope.Should().BeNull();
    }

    [Fact]
    public void Matrikkelenheter_beholdes()
    {
        // Ikke en områdedimensjon i bufferen, men et filter brukeren faktisk har satt.
        FacetFilters.ForAreaFacet(Fullt()).CadastralUnitIds.Should().Equal("1101-1/2");
    }

    [Fact]
    public void Alle_andre_felt_kopieres()
    {
        var kilde = Fullt();
        var resultat = FacetFilters.ForAreaFacet(kilde);

        var mangler = new List<string>();

        foreach (var property in typeof(IObservationFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (FacetFilters.DeliberatelyOmitted.Contains(property.Name))
                continue;

            var forventet = property.GetValue(kilde);
            var faktisk = property.GetValue(resultat);

            if (!Equals(forventet, faktisk))
                mangler.Add(property.Name);
        }

        mangler.Should().BeEmpty(
            "hvert felt på IObservationFilter må enten kopieres i ForAreaFacet eller stå " +
            "i DeliberatelyOmitted. Et glemt felt gir for høye tall i områdefasetten.");
    }

    [Fact]
    public void De_utelatte_er_bare_geografi()
    {
        // Vokter listen selv: utvides den uten videre, slutter testen over å bety noe.
        FacetFilters.DeliberatelyOmitted.Should().BeEquivalentTo(
            ["MunicipalityIds", "CountyIds", "RestrictedAreaIds", "OceanAreaIds"]);
    }
}

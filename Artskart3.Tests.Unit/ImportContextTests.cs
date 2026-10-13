using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportContextTests
{
    [Fact]
    public void AppendDynamicProperty_legger_inn_ny_nokkel()
    {
        var context = new ImportContext();

        context.AppendDynamicProperty("alfa", "1");

        context.DynamicProperties["alfa"].Should().Be("1");
    }

    [Fact]
    public void AppendDynamicProperty_foyer_paa_med_loddrett_strek()
    {
        var context = new ImportContext();

        context.AppendDynamicProperty("alfa", "1");
        context.AppendDynamicProperty("alfa", "2");

        context.DynamicProperties["alfa"].Should().Be("1|2");
    }

    /// <summary>Samme verdi to ganger skal ikke dubleres.</summary>
    [Fact]
    public void AppendDynamicProperty_gjentar_ikke_samme_verdi()
    {
        var context = new ImportContext();

        context.AppendDynamicProperty("alfa", "1");
        context.AppendDynamicProperty("alfa", "1");
        context.AppendDynamicProperty("alfa", "2");
        context.AppendDynamicProperty("alfa", "1");

        context.DynamicProperties["alfa"].Should().Be("1|2");
    }

    [Fact]
    public void AppendDynamicProperty_hopper_over_null_for_en_ny_nokkel()
    {
        var context = new ImportContext();

        context.AppendDynamicProperty("alfa", null);

        context.DynamicProperties.Should().NotContainKey("alfa");
    }

    /// <summary>
    /// En nøkkel som allerede står med <c>null</c> blir overskrevet, ikke føyd på.
    /// Semantikken er Artskart 2s.
    /// </summary>
    [Fact]
    public void AppendDynamicProperty_overskriver_en_nokkel_som_star_som_null()
    {
        var context = new ImportContext();
        context.DynamicProperties["alfa"] = null;

        context.AppendDynamicProperty("alfa", "1");

        context.DynamicProperties["alfa"].Should().Be("1");
    }

    [Fact]
    public void SaveOriginalValue_prefikser_nokkelen()
    {
        var context = new ImportContext();

        context.SaveOriginalValue("Latitude", "59.9139");

        context.DynamicProperties["_original_Latitude"].Should().Be("59.9139");
        context.HasOriginalValue("Latitude").Should().BeTrue();
        context.HasOriginalValue("Longitude").Should().BeFalse();
    }

    [Fact]
    public void RemoveDynamicProperty_fjerner_nokkelen()
    {
        var context = new ImportContext();
        context.AppendDynamicProperty("alfa", "1");

        context.RemoveDynamicProperty("alfa");

        context.DynamicProperties.Should().BeEmpty();
    }
}

public class ImportedObservationTests
{
    [Fact]
    public void AddError_setter_HasErrors_og_legger_til_raden()
    {
        var observasjon = new ImportedObservation();

        observasjon.AddError(RecordValidationCode.BasisOfRecordValueUnknown, "ukjent verdi: foo");

        observasjon.HasErrors.Should().BeTrue();
        observasjon.Errors.Should().HaveCount(1);
        observasjon.Errors[0].ValidationCode.Should().Be(RecordValidationCode.BasisOfRecordValueUnknown);
        observasjon.Errors[0].ErrorContext.Should().Be("ukjent verdi: foo");
    }

    [Fact]
    public void Tagger_legges_til_en_gang_og_kan_fjernes()
    {
        var observasjon = new ImportedObservation();

        observasjon.AddTag(Tag.Absent);
        observasjon.AddTag(Tag.Absent);
        observasjon.AddTag(Tag.QualityIssue);

        observasjon.Tags.Should().HaveCount(2);
        observasjon.HasTag(Tag.Absent).Should().BeTrue();

        observasjon.RemoveTag(Tag.Absent);

        observasjon.HasTag(Tag.Absent).Should().BeFalse();
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
    }

    /// <summary>
    /// Artskart 2 bruker <c>+=</c> uten skilletegn. Hvert kallsted bærer sin egen
    /// avstand, og teksten glir derfor sammen med det som sto der fra før.
    /// </summary>
    [Fact]
    public void AppendNotes_foyer_paa_uten_skilletegn()
    {
        var observasjon = new ImportedObservation();

        observasjon.AppendNotes(null);
        observasjon.AppendNotes(string.Empty);
        observasjon.Notes.Should().BeNull();

        observasjon.AppendNotes("Mangler koordinat. ");
        observasjon.AppendNotes("Satt til kommunesenter.");

        observasjon.Notes.Should().Be("Mangler koordinat. Satt til kommunesenter.");
    }

    [Fact]
    public void RegistrationStatusId_er_funnet_som_standard()
    {
        new ImportedObservation().RegistrationStatusId.Should().Be(1);
    }
}

public class ImportRecordResultTests
{
    [Theory]
    [InlineData(RecordValidationCode.Ok, true, false)]
    [InlineData(RecordValidationCode.Warn, true, true)]
    [InlineData(RecordValidationCode.SensitiveSpecies, true, false)]
    [InlineData(RecordValidationCode.Error, false, false)]
    [InlineData(RecordValidationCode.DuplicateId, false, false)]
    public void Godtar_bare_de_tre_kodene_som_lar_posten_fortsette(
        RecordValidationCode kode, bool godtatt, bool advarsel)
    {
        var resultat = kode switch
        {
            RecordValidationCode.Ok => ImportRecordResult.Ok(),
            RecordValidationCode.Warn => ImportRecordResult.Warn(RecordNotificationCode.Ok, ""),
            RecordValidationCode.SensitiveSpecies => ImportRecordResult.Sensitive(RecordNotificationCode.Ok),
            _ => ImportRecordResult.Rejected(kode, RecordNotificationCode.Ok, "")
        };

        resultat.IsAccepted.Should().Be(godtatt);
        resultat.IsWarning.Should().Be(advarsel);
    }

    /// <summary>
    /// <c>ErrorContext</c> er <c>NVARCHAR(400) NOT NULL</c>, så trunkeringen må skje
    /// før verdien forlater importen.
    /// </summary>
    [Fact]
    public void Trunkerer_ErrorContext_til_400_tegn()
    {
        var lang = new string('x', 500);

        var resultat = ImportRecordResult.Rejected(
            RecordValidationCode.Error, RecordNotificationCode.Ok, lang);

        resultat.ErrorContext.Should().HaveLength(400);
        resultat.ErrorContext.Should().EndWith("...");
    }

    [Fact]
    public void Ok_har_tom_feilkontekst()
    {
        ImportRecordResult.Ok().ErrorContext.Should().BeEmpty();
    }

    /// <summary>
    /// Tallverdiene ligger i basen. En omsortering av enumen ville gjort alle lagrede
    /// koder feil uten at noe feiler.
    /// </summary>
    [Fact]
    public void Valideringskodene_har_de_samme_tallverdiene_som_i_Artskart2()
    {
        ((int)RecordValidationCode.Ok).Should().Be(0);
        ((int)RecordValidationCode.Warn).Should().Be(1);
        ((int)RecordValidationCode.InstitutionCodeNotKnown).Should().Be(4);
        ((int)RecordValidationCode.CatalogNumberIsMissing).Should().Be(6);
        ((int)RecordValidationCode.NoTaxonomicMatch).Should().Be(12);
        ((int)RecordValidationCode.SensitiveSpecies).Should().Be(16);
        ((int)RecordValidationCode.Annotation).Should().Be(18);
    }

    [Fact]
    public void Varselkodene_har_de_samme_tallverdiene_som_i_Artskart2()
    {
        ((int)RecordNotificationCode.Ok).Should().Be(0);
        ((int)RecordNotificationCode.InvalidMgrsNotation).Should().Be(3);
        ((int)RecordNotificationCode.NoHitOnRequiredAreadatasets).Should().Be(4);
        ((int)RecordNotificationCode.NoTaxonGroupHit).Should().Be(7);
        ((int)RecordNotificationCode.StateProvinceIsJanMayenButCoordinateElsewhere).Should().Be(17);
        ((int)RecordNotificationCode.DatasetShouldBeMasked).Should().Be(20);
    }
}

using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportCheckValidDatesStepTests
{
    private static readonly DateTime IDag = new(2026, 10, 13, 11, 30, 0);

    /// <summary>Fast klokke i UTC, slik at «i dag» er den samme i hver kjøring.</summary>
    private sealed class FastKlokke(DateTime nå) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nå, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly ImportCheckValidDatesStep Steg = new(new FastKlokke(IDag));

    private static ImportRecordResult Kjør(ImportedObservation observasjon) =>
        Steg.ValidateAndImport(new CacheRecord(), observasjon, new ImportContext());

    private static ImportedObservation Observasjon(
        DateTime? samlet = null,
        DateTime? bestemt = null,
        DateTime? sistEndret = null) => new()
    {
        DateTimeCollected = samlet ?? new DateTime(2020, 6, 1),
        DatetimeIdentified = bestemt,
        DateLastModified = sistEndret ?? new DateTime(2021, 1, 1)
    };

    /// <summary>Som <see cref="Observasjon"/>, men uten innsamlingsdato i det hele tatt.</summary>
    private static ImportedObservation UtenInnsamlingsdato() => new()
    {
        DateTimeCollected = null,
        DateLastModified = new DateTime(2021, 1, 1)
    };

    [Fact]
    public void Er_et_post_steg()
    {
        Steg.StepType.Should().Be(ImportStepType.Post);
    }

    [Fact]
    public void Godtar_rimelige_datoer_uten_a_merke_noe()
    {
        var observasjon = Observasjon(bestemt: new DateTime(2020, 7, 1));

        var resultat = Kjør(observasjon);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.HasErrors.Should().BeFalse();
        observasjon.Errors.Should().BeEmpty();
        observasjon.Tags.Should().BeEmpty();
        observasjon.Notes.Should().BeNull();
    }

    /// <summary>
    /// Manglende innsamlingsdato avviser ikke posten — den merkes. En observasjon uten
    /// dato er fortsatt en observasjon.
    /// </summary>
    [Fact]
    public void Merker_manglende_innsamlingsdato_uten_a_avvise()
    {
        var observasjon = UtenInnsamlingsdato();

        var resultat = Kjør(observasjon);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.HasErrors.Should().BeTrue();
        observasjon.Errors.Should().ContainSingle(x => x.ValidationCode == RecordValidationCode.NoDatetime);
        observasjon.Errors[0].ErrorContext.Should().Be("DateTimeCollected");
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        observasjon.Notes.Should().Be("Mangler år DateTimeCollected");
    }

    [Fact]
    public void Avviser_innsamlingsdato_i_framtiden()
    {
        var resultat = Kjør(Observasjon(samlet: IDag.AddDays(1)));

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.DatetimeOutOfRange);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.DateTimeCollectedError);
        resultat.ErrorContext.Should().Be("DateTimeCollected is in the future : 14.10.2026 11:30:00");
    }

    /// <summary>
    /// «I dag» er datoen, ikke klokkeslettet: en post samlet inn tidligere i dag er
    /// ikke i framtiden.
    /// </summary>
    [Fact]
    public void Godtar_innsamlingsdato_tidligere_i_dag()
    {
        Kjør(Observasjon(samlet: IDag.Date)).ValidationCode.Should().Be(RecordValidationCode.Ok);
    }

    /// <summary>
    /// Gamle datoer er ofte ekte — herbariebelegg går tilbake til 1600-tallet. De
    /// merkes, men avvises ikke.
    /// </summary>
    [Fact]
    public void Merker_innsamlingsdato_for_1700_uten_a_avvise()
    {
        var observasjon = Observasjon(samlet: new DateTime(1650, 5, 1));

        var resultat = Kjør(observasjon);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.HasErrors.Should().BeTrue();
        observasjon.Errors.Should().ContainSingle(x => x.ValidationCode == RecordValidationCode.DatetimeOutOfRange);
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        // Formatet er nb-NO, målt mot de 61 millioner radene som alt ligger i index.
        observasjon.Notes.Should().Be("Veldig gammel DateTimeCollected: 01.05.1650 00:00:00 ");
        observasjon.Errors[0].ErrorContext.Should().Be("DateTimeCollected: 01.05.1650");
    }

    [Fact]
    public void Godtar_datoer_fra_og_med_1700()
    {
        var observasjon = Observasjon(samlet: new DateTime(1700, 1, 1));

        Kjør(observasjon).ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Merker_bestemmelsesdato_for_1700()
    {
        var observasjon = Observasjon(bestemt: new DateTime(1650, 1, 1));

        Kjør(observasjon).ValidationCode.Should().Be(RecordValidationCode.Ok);

        observasjon.Errors.Should().ContainSingle();
        observasjon.Errors[0].ErrorContext.Should().Be("DatetimeIdentified: 01.01.1650");
    }

    [Fact]
    public void Avviser_bestemmelsesdato_i_framtiden()
    {
        var resultat = Kjør(Observasjon(bestemt: IDag.AddDays(1)));

        resultat.IsAccepted.Should().BeFalse();
        resultat.NotificationCode.Should().Be(RecordNotificationCode.DateTimeIdentifiedError);
    }

    /// <summary>
    /// <c>DateTime.MinValue</c> betyr «ikke satt», på linje med <c>null</c>. Uten det
    /// ville hver post uten bestemmelsesdato blitt merket som veldig gammel.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("0001-01-01")]
    public void Merker_ikke_manglende_bestemmelsesdato(string? dato)
    {
        var observasjon = Observasjon(bestemt: dato is null ? null : DateTime.Parse(dato));

        Kjør(observasjon).ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.Errors.Should().BeEmpty();
        observasjon.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Merker_sist_endret_for_1700()
    {
        var observasjon = Observasjon(sistEndret: new DateTime(1650, 1, 1));

        Kjør(observasjon).ValidationCode.Should().Be(RecordValidationCode.Ok);

        observasjon.Errors.Should().ContainSingle();
        observasjon.Errors[0].ErrorContext.Should().Be("DateLastModified: 01.01.1650");
    }

    /// <summary>
    /// En kilde med feil klokke skal ikke miste postene sine: sist endret i framtiden
    /// gir advarsel, ikke avvisning.
    /// </summary>
    [Fact]
    public void Advarer_om_sist_endret_i_framtiden_uten_a_avvise()
    {
        var resultat = Kjør(Observasjon(sistEndret: IDag.AddDays(1)));

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.DateLastModifiedError);
        resultat.IsAccepted.Should().BeTrue();
    }

    /// <summary>
    /// Meldingene glir sammen med det som allerede sto i <c>Notes</c>, fordi Artskart 2
    /// bruker <c>+=</c> uten skilletegn.
    /// </summary>
    [Fact]
    public void Foyer_meldingen_paa_eksisterende_notes()
    {
        var observasjon = UtenInnsamlingsdato();
        observasjon.Notes = "Fra kilden.";

        Kjør(observasjon);

        observasjon.Notes.Should().Be("Fra kilden.Mangler år DateTimeCollected");
    }

    [Fact]
    public void Merker_flere_datoer_i_samme_kjoring()
    {
        var observasjon = Observasjon(
            samlet: new DateTime(1650, 1, 1),
            bestemt: new DateTime(1660, 1, 1),
            sistEndret: new DateTime(1670, 1, 1));

        Kjør(observasjon).ValidationCode.Should().Be(RecordValidationCode.Ok);

        observasjon.Errors.Should().HaveCount(3);
        observasjon.Tags.Should().HaveCount(1, "samme tagg legges bare til én gang");
    }
}

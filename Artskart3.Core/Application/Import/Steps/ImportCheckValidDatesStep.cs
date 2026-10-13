using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Kontrollerer datoene på den ferdig tolkede observasjonen. Port av Artskart 2s
/// <c>ImportCheckValidDates</c>.
///
/// LESER OBSERVASJONEN, IKKE POSTEN. Datoene er satt av
/// <c>ImportSimpleProperties</c> før dette steget; her vurderes bare om de er
/// rimelige.
///
/// TO UTFALL, OG SKILLET ER IKKE OPPLAGT:
/// <list type="bullet">
/// <item>En dato i framtiden avviser posten — unntatt <c>DateLastModified</c>, som
/// bare gir en advarsel.</item>
/// <item>En dato før 1700, eller manglende innsamlingsdato, merker observasjonen
/// (<c>HasErrors</c>, feilrad, <c>Tag.QualityIssue</c>, tekst i <c>Notes</c>) men
/// avviser den ikke. Gamle datoer er ofte ekte: herbariebelegg går tilbake til
/// 1600-tallet.</item>
/// </list>
///
/// «I DAG» ER LOKAL TID, slik Artskart 2 har det (<c>DateTime.Today</c>). En post
/// samlet inn «i dag» i en kilde en tidssone unna kan derfor være i framtiden hos oss.
/// Tidskilden er injisert for at en test skal kunne sette datoen.
/// </summary>
public class ImportCheckValidDatesStep(TimeProvider? timeProvider = null) : IImportStep
{
    /// <summary>
    /// Alt som er eldre enn dette regnes som mistenkelig. Verdien er Artskart 2s.
    /// </summary>
    private static readonly DateTime EldsteRimeligeDato = new(1700, 1, 1);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public ImportStepType StepType => ImportStepType.Post;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var nå = _timeProvider.GetLocalNow();
        var iDag = nå.Date;

        if (observation.DateTimeCollected is null)
        {
            MarkerManglende(observation, nameof(observation.DateTimeCollected));
        }

        if (observation.DateTimeCollected > iDag)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.DatetimeOutOfRange,
                RecordNotificationCode.DateTimeCollectedError,
                "DateTimeCollected is in the future : " + ImportTextFormat.DateAndTime(observation.DateTimeCollected));
        }

        if (observation.DateTimeCollected < EldsteRimeligeDato)
        {
            MarkerGammel(observation, nameof(observation.DateTimeCollected), observation.DateTimeCollected);
        }

        // DateTime.MinValue teller som «ikke satt» i tillegg til null: Artskart 2 har
        // sett begge, og en MinValue-dato ville ellers blitt merket som veldig gammel
        // for hver post som mangler bestemmelsesdato.
        var harBestemmelsesdato = observation.DatetimeIdentified is not null
                                  && observation.DatetimeIdentified != DateTime.MinValue;

        if (harBestemmelsesdato && observation.DatetimeIdentified < EldsteRimeligeDato)
        {
            MarkerGammel(observation, nameof(observation.DatetimeIdentified), observation.DatetimeIdentified);
        }

        if (harBestemmelsesdato && observation.DatetimeIdentified > iDag)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.DatetimeOutOfRange,
                RecordNotificationCode.DateTimeIdentifiedError,
                "DateTimeIdentified is in the future : " + ImportTextFormat.DateAndTime(observation.DatetimeIdentified));
        }

        if (observation.DateLastModified < EldsteRimeligeDato)
        {
            MarkerGammel(observation, nameof(observation.DateLastModified), observation.DateLastModified);
        }

        // Advarsel, ikke avvisning: en kilde med feil klokke skal ikke miste postene
        // sine. Sammenligningen er mot klokkeslettet, ikke mot datoen, slik Artskart 2
        // har det.
        if (observation.DateLastModified > nå.DateTime)
        {
            return ImportRecordResult.Warn(
                RecordNotificationCode.DateLastModifiedError,
                "DateLastModified is in the future : " + ImportTextFormat.DateAndTime(observation.DateLastModified.ToUniversalTime()));
        }

        return ImportRecordResult.Ok();
    }

    private static void MarkerGammel(ImportedObservation observation, string feltnavn, DateTime? dato)
    {
        observation.AppendNotes($"Veldig gammel {feltnavn}: {ImportTextFormat.DateAndTime(dato)} ");
        observation.AddError(
            RecordValidationCode.DatetimeOutOfRange,
            $"{feltnavn}: {ImportTextFormat.ShortDate(dato)}");
        observation.AddTag(Tag.QualityIssue);
    }

    private static void MarkerManglende(ImportedObservation observation, string feltnavn)
    {
        // Ingen mellomrom bakerst — slik Artskart 2 har det. Teksten glir derfor sammen
        // med en påfølgende melding.
        observation.AppendNotes($"Mangler år {feltnavn}");
        observation.AddError(RecordValidationCode.NoDatetime, feltnavn);
        observation.AddTag(Tag.QualityIssue);
    }
}

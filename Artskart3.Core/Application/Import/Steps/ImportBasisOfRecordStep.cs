using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Setter funntypen — om posten er en observasjon, et museumsobjekt, en litteraturkilde
/// og så videre. Port av Artskart 2s <c>ImportBasisOfRecord</c>.
///
/// EN UKJENT VERDI AVVISER IKKE POSTEN. Artskart 2 har kodene
/// <c>BasisOfRecordMissing</c> og <c>BasisOfRecordValueUnknown</c>, og begge
/// avvisningene er kommentert ut i koden. Det som skjer i stedet er at posten merkes:
/// funntypen blir «unrecognised», observasjonen får <c>HasErrors</c>, en
/// <c>ObservationError</c> og taggen <c>QualityIssue</c>, kildens verdi tas vare på i
/// <c>_original_BasisOfRecord</c>, og steget svarer <c>Warn</c>. Posten blir altså
/// importert og synlig — med et kvalitetsflagg.
///
/// MELDINGEN SKRIVES PÅ <c>record.Notes</c>, ikke på observasjonen, fordi
/// <c>ImportSimplePropertiesStep</c> kopierer feltet senere. Skrev vi direkte på
/// observasjonen, ville den kopieringen overskrevet meldingen.
/// </summary>
public class ImportBasisOfRecordStep : IImportStep
{
    /// <summary>Raden ugjenkjennelige verdier havner på. Id -1 i <c>BasisOfRecord</c>.</summary>
    private const string Ugjenkjent = "unrecognised";

    private readonly IReferenceValueRegistry _register;
    private readonly IReferenceVariantWriter? _variantskriver;

    public ImportBasisOfRecordStep(
        IReferenceValueRegistry register,
        IReferenceVariantWriter? variantskriver = null)
    {
        _register = register;
        _variantskriver = variantskriver;
    }

    public ImportStepType StepType => ImportStepType.Pre;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        var verdi = (record.BasisOfRecord ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(record.BasisOfRecord))
        {
            MerkDårlig(record, observation, "Mangler BasisOfRecord", record.BasisOfRecord);

            return Ugjenkjennelig(record, observation, context, verdi);
        }

        // Eksakt treff på navnet.
        //
        // «unrecognised» TREFFER HER, OG DET ER EN FELLE I ARTSKART 2: vakten mot
        // magiske verdier sammenligner med «basisofrecords/unrecognised», altså
        // RavenDB-id-en med prefiks, som ingen kilde sender. En kilde som skriver
        // «unrecognised» får derfor funntypen «unrecognised» med Ok og uten
        // kvalitetsflagg — stikk i strid med det den utkommenterte koden ville gjort.
        // Her er vakten på verdien, slik hensikten var.
        if (string.Equals(verdi, Ugjenkjent, StringComparison.Ordinal))
        {
            MerkDårlig(record, observation, $"BasisOfRecord ikke gjenkjent {record.BasisOfRecord}", record.BasisOfRecord);

            return Ugjenkjennelig(record, observation, context, verdi);
        }

        var treff = _register.ByName(ReferenceValueKind.BasisOfRecord, verdi);

        if (treff is not null)
        {
            observation.BasisOfRecordId = treff.Id;

            return ImportRecordResult.Ok();
        }

        // Så variantene — skrivemåtene kildene faktisk bruker.
        treff = _register.ByVariant(ReferenceValueKind.BasisOfRecord, verdi);

        if (treff is not null)
        {
            observation.BasisOfRecordId = treff.Id;

            return ImportRecordResult.Ok();
        }

        MerkDårlig(record, observation, $"BasisOfRecord ikke gjenkjent {record.BasisOfRecord}", record.BasisOfRecord);

        return Ugjenkjennelig(record, observation, context, verdi);
    }

    /// <summary>
    /// Funntypen blir «unrecognised», kildens verdi tas vare på, og verdien føres opp i
    /// variantlisten på den raden — en logg over hva kildene sender.
    /// </summary>
    private ImportRecordResult Ugjenkjennelig(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        string verdi)
    {
        var ugjenkjent = _register.Unrecognised(ReferenceValueKind.BasisOfRecord);

        if (ugjenkjent is not null) observation.BasisOfRecordId = ugjenkjent.Id;

        // TOM VERDI FØRES IKKE OPP. Artskart 2 gjør det, og sporet står i dataene: rad
        // -1 har variantlisten «unknown,» — det siste leddet er den tomme strengen, lagt
        // inn av poster uten funntype i det hele tatt. Den opplyser ingen om noe.
        if (verdi.Length > 0)
        {
            _variantskriver?.RecordUnrecognisedValue(ReferenceValueKind.BasisOfRecord, verdi);
        }

        if (!string.IsNullOrWhiteSpace(record.BasisOfRecord))
        {
            context.SaveOriginalValue(nameof(CacheRecord.BasisOfRecord), record.BasisOfRecord);
        }

        return ImportRecordResult.Warn(RecordNotificationCode.Ok, record.Notes ?? string.Empty);
    }

    private static void MerkDårlig(
        CacheRecord record,
        ImportedObservation observation,
        string melding,
        string? kildeverdi)
    {
        record.Notes += melding;

        observation.AddError(
            RecordValidationCode.BasisOfRecordValueUnknown,
            ImportValueHelper.Left(kildeverdi ?? string.Empty, 400));

        observation.AddTag(Tag.QualityIssue);
    }
}

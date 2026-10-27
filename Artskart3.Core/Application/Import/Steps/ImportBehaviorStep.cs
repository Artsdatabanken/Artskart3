using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Setter atferden — om individet beveget seg, spiste, var dødt, hvilte. Port av
/// Artskart 2s <c>ImportBehavior</c>.
///
/// FELTET ER FLERVERDI. Kilden kan sende «moving,feeding» eller «hvilende;soling», og
/// posten får én atferd per verdi som lar seg gjenkjenne.
///
/// MELLOMROM FJERNES FØR OPPSLAG, og det har en konsekvens verdt å vite om: varianter
/// som inneholder mellomrom kan aldri treffe. Tabellen har noen slike — «funnet dod» på
/// <c>dead</c> — og de er døde oppføringer i dagens løsning også.
///
/// EN UKJENT VERDI GIR INGEN ATFERD, ikke en avvisning. Posten merkes i stedet:
/// <c>HasErrors</c>, en <c>ObservationError</c> per ukjent verdi, taggen
/// <c>QualityIssue</c>, kildens verdi i <c>_original_Behavior</c>, og <c>Warn</c> som
/// svar. Til forskjell fra funntypen settes ingen «unrecognised»-rad på observasjonen —
/// <c>Behavior</c> har ingen slik rad, og trenger den ikke.
/// </summary>
public class ImportBehaviorStep : IImportStep
{
    private const string Ugjenkjent = "unrecognised";

    /// <summary>
    /// Skilletegnene kildene bruker mellom flere atferder.
    /// </summary>
    private static readonly string[] Skilletegn = [",", ";", "|"];

    /// <summary>
    /// Noen kilder sender hele DwC-feltnavnet med verdien: «...,behavior:feeding».
    /// Artskart 2 kaller det «mdir special case».
    /// </summary>
    private const string Feltprefiks = "behavior:";

    private readonly IReferenceValueRegistry _register;
    private readonly IReferenceVariantWriter? _variantskriver;

    public ImportBehaviorStep(
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

        if (string.IsNullOrWhiteSpace(record.Behavior)) return ImportRecordResult.Ok();

        var tekst = record.Behavior.Trim().ToLowerInvariant().Replace(" ", string.Empty);

        var verdier = DelOpp(record, tekst);

        var noeUkjent = false;

        foreach (var verdi in verdier)
        {
            // «unrecognised» er en magisk verdi ingen kilde skal sende.
            if (string.Equals(verdi, Ugjenkjent, StringComparison.Ordinal))
            {
                MerkDårlig(record, observation, verdi);
                noeUkjent = true;
                continue;
            }

            var treff = _register.ByName(ReferenceValueKind.Behavior, verdi)
                        ?? _register.ByVariant(ReferenceValueKind.Behavior, verdi);

            if (treff is not null)
            {
                // Samme atferd to ganger i samme felt gir én rad.
                //
                // KOLONNEN ER tinyint, så id-en må ligge mellom 1 og 255. Alle de seks
                // radene gjør det. Skulle en rad ligge utenfor, er det bedre at posten
                // mangler atferden enn at den får en annen — (byte)300 er 44.
                if (treff.Id is > 0 and <= byte.MaxValue && !observation.BehaviorIds.Contains((byte)treff.Id))
                {
                    observation.BehaviorIds.Add((byte)treff.Id);
                }

                continue;
            }

            _variantskriver?.RecordUnrecognisedValue(ReferenceValueKind.Behavior, verdi);

            MerkDårlig(record, observation, verdi);
            noeUkjent = true;
        }

        if (!noeUkjent) return ImportRecordResult.Ok();

        context.SaveOriginalValue(nameof(CacheRecord.Behavior), record.Behavior);

        return ImportRecordResult.Warn(RecordNotificationCode.Ok, record.Notes ?? string.Empty);
    }

    /// <summary>
    /// Deler feltet i enkeltverdier.
    ///
    /// TO AVVIK FRA ARTSKART 2, BEGGE KRASJ: den tar <c>[1]</c> av oppdelingen i
    /// <c>behavior:</c>-tilfellet, så en verdi som <em>begynner</em> med
    /// <c>behavior:</c> gir bare ett ledd og en <c>IndexOutOfRangeException</c> —
    /// og den leser <c>record.Notes.Contains(...)</c> uten å sjekke for <c>null</c>,
    /// som er standardverdien. Begge feller hele nodeimporten. Her tas første ledd som
    /// har innhold, og null-sjekken er på plass.
    /// </summary>
    private static List<string> DelOpp(CacheRecord record, string tekst)
    {
        if (!tekst.Contains(Feltprefiks, StringComparison.Ordinal))
        {
            return tekst.Split(Skilletegn, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        var ledd = tekst
            .Split([Feltprefiks, ","], StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (record.Notes is null || !record.Notes.Contains(record.Behavior!, StringComparison.Ordinal))
        {
            record.Notes += $"Behavior: '{record.Behavior}'. ";
        }

        // Artskart 2 tar leddet etter prefikset. Står prefikset først, finnes det
        // leddet ikke, og da er det første leddet verdien.
        return ledd.Length > 1 ? [ledd[1]] : [.. ledd];
    }

    private static void MerkDårlig(CacheRecord record, ImportedObservation observation, string verdi)
    {
        record.Notes += $"Behavior ikke gjenkjent '{verdi}'. ";

        observation.AddError(
            RecordValidationCode.BehaviorValueUnknown,
            ImportValueHelper.Left(verdi, 400));

        observation.AddTag(Tag.QualityIssue);
    }
}

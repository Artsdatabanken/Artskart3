using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Knytter mediefiler til observasjonen. Port av Artskart 2s <c>ImportMedia</c>.
///
/// LASTER IKKE NED NOE. Steget registrerer URL-en og typen; nedlastingen er en egen
/// jobb. <c>Downloaded</c> er <c>false</c>, <c>Size</c> er 0 og <c>Image</c> er tom —
/// nøyaktig som i Artskart 2, der nedlastingskoden står utkommentert.
///
/// TYPEN ER Parallel, IKKE Post. Planen (1.4) fører <c>ImportMedia</c> som PostTask
/// og sier at <c>ImportGeometry</c> er den eneste parallelle. Koden sier noe annet:
/// <c>ImportMedia</c>-konstruktøren setter <c>ParallellTask</c>. Steget kjører altså
/// FØR <c>ImportSimpleProperties</c>, ikke etter — og det har en konsekvens, se under.
///
/// TRE TING SOM ER BEVART FORDI DE ER DAGENS OPPFØRSEL:
///
/// 1. <c>associatedMedia</c> med <c>|</c> i seg hoppes over i sin helhet. Betingelsen
///    er «ikke tom OG inneholder ikke <c>|</c>», mens splittingen like etterpå deler
///    på både <c>;</c> og <c>|</c>. En kilde som leverer flere URL-er skilt med
///    loddrett strek får dem altså ikke med fra dette feltet. <c>multimedia</c>-
///    utvidelsen rammes ikke.
/// 2. Typegjenkjenningen ser på hele identifikatorstrengen, ikke på hver URL for seg.
///    Inneholder den «jpg» én gang, behandles alle URL-ene i den som bilder. Og testen
///    er <c>Contains</c>, ikke filendelse: en URL mot et domene med «png» i navnet
///    teller som bilde.
/// 3. Det som verken er bilde eller lyd havner i <c>AssociatedReferences</c> — og for
///    node 40 og 211 blir det overskrevet av <c>ImportSimpleProperties</c> etterpå, som
///    setter GBIF-URL-en med tilordning i stedet for union. For de to største
///    GBIF-nodene forsvinner altså mediereferanser som ikke er bilde eller lyd.
/// </summary>
public class ImportMediaStep : IImportStep
{
    /// <summary>Gjenkjennes på innhold i identifikatoren, ikke på filendelse.</summary>
    private static readonly string[] Bildetyper = ["jpg", "jpeg", "png", "gif", "jp2", "dng", "tiff"];

    private static readonly string[] Bildeformater = ["image/jpeg", "image/png"];

    private static readonly string[] Lydtyper = ["mp3", "mpeg"];

    /// <summary><c>MediaFileType.Id</c> 1 = Jpeg, 2 = Audio.</summary>
    private const int JpegTypeId = 1;

    private const int AudioTypeId = 2;

    public ImportStepType StepType => ImportStepType.Parallel;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);

        var harAssociatedMedia = !string.IsNullOrWhiteSpace(record.AssociatedMedia);
        var harMultimedia = record.Multimedias is { Length: > 0 };

        if (!harAssociatedMedia && !harMultimedia) return ImportRecordResult.Ok();

        var mediaelementer = SamleMediaelementer(record, harAssociatedMedia);

        foreach (var element in mediaelementer)
        {
            var erBilde = HarBildetype(element) || HarBildeformat(element);
            var erLyd = !erBilde && HarLydtype(element);

            foreach (var url in SplittPåSkilletegn(element.Identifier))
            {
                if (erBilde)
                {
                    observation.MediaFiles.Add(LagMediefil(element, url, JpegTypeId));
                    observation.AddTag(Tag.HasImage);
                }
                else if (erLyd)
                {
                    observation.MediaFiles.Add(LagMediefil(element, url, AudioTypeId));
                }
                else
                {
                    // Førstedelen av identifikatoren, ikke URL-en vi står på. Slik er
                    // det i Artskart 2, og det betyr at bare den første URL-en i en
                    // sammensatt streng blir referanse.
                    observation.AddAssociatedReference(element.Identifier?.Split('|').First());
                }
            }
        }

        return ImportRecordResult.Ok();
    }

    /// <summary>
    /// Slår sammen <c>associatedMedia</c> og <c>multimedia</c>-utvidelsen.
    /// Multimedia-oppføringer legges bare til hvis identifikatoren ikke alt finnes —
    /// sammenligningen er uten hensyn til bokstavstørrelse og med trimming.
    /// </summary>
    private static List<CacheMultimedia> SamleMediaelementer(CacheRecord record, bool harAssociatedMedia)
    {
        var elementer = new List<CacheMultimedia>();

        // Punkt 1 i klassekommentaren: strengen hoppes over hvis den inneholder «|».
        if (harAssociatedMedia && !record.AssociatedMedia!.Contains('|'))
        {
            elementer.AddRange(SplittPåSkilletegn(record.AssociatedMedia)
                .Select(url => new CacheMultimedia { Identifier = url }));
        }

        foreach (var media in record.Multimedias ?? [])
        {
            if (string.IsNullOrWhiteSpace(media.Identifier)) continue;

            var finnesAlt = elementer.Any(x =>
                string.Equals(x.Identifier?.Trim(), media.Identifier.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!finnesAlt) elementer.Add(media);
        }

        return elementer;
    }

    private static ImportedMediaFile LagMediefil(CacheMultimedia element, string url, int mediaFileTypeId) => new()
    {
        Origin = url,
        MediaFileTypeId = mediaFileTypeId,
        License = ImportValueHelper.DotLeft(element.License, 500),
        RightsHolder = ImportValueHelper.DotLeft(Rettighetshaver(element), 500),
        Description = ImportValueHelper.DotLeft(element.Description, 500)
    };

    /// <summary>
    /// Rettighetshaver, med kilde bak komma. Faller tilbake fra <c>rightsHolder</c> til
    /// <c>creator</c> til <c>publisher</c> — og ender som bare «, kilde» hvis ingen av
    /// de tre er utfylt, slik Artskart 2 gjør det.
    /// </summary>
    private static string Rettighetshaver(CacheMultimedia element)
    {
        var eier = !string.IsNullOrWhiteSpace(element.RightsHolder) ? element.RightsHolder
            : !string.IsNullOrWhiteSpace(element.Creator) ? element.Creator
            : !string.IsNullOrWhiteSpace(element.Publisher) ? element.Publisher
            : string.Empty;

        var kilde = !string.IsNullOrWhiteSpace(element.Source) ? ", " + element.Source : string.Empty;

        return eier + kilde;
    }

    /// <summary>Deler på <c>;</c> og <c>|</c> og trimmer, som <c>ConcanatedStringToArray</c>.</summary>
    private static string[] SplittPåSkilletegn(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

    private static bool HarBildetype(CacheMultimedia element) =>
        element.Identifier is not null
        && Bildetyper.Any(x => element.Identifier.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static bool HarBildeformat(CacheMultimedia element) =>
        element.Format is not null
        && Bildeformater.Any(x => element.Format.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static bool HarLydtype(CacheMultimedia element) =>
        element.Identifier is not null
        && Lydtyper.Any(x => element.Identifier.Contains(x, StringComparison.OrdinalIgnoreCase));
}

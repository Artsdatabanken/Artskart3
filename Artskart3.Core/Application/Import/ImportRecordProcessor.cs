using System.Text.Json;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Kjører steg-kjeden over én post: <see cref="CacheRecord"/> inn,
/// <see cref="ImportedObservation"/> ut. Skriver ingenting noe sted.
///
/// Port av løkkene i Artskart 2s <c>RecordsProcessor.ProcessRecord</c>, med de samme
/// tre fasene og den samme stoppregelen:
///
/// <list type="number">
/// <item><see cref="ImportStepType.Pre"/> sekvensielt — første avvisning stopper posten</item>
/// <item><see cref="ImportStepType.Parallel"/> samtidig — alle kjører ferdig, så stopper posten hvis noen avviste</item>
/// <item><see cref="ImportStepType.Post"/> sekvensielt — første avvisning stopper posten</item>
/// </list>
///
/// REKKEFØLGEN INNENFOR EN FASE ER DEN STEGENE BLE GITT I. Den er en del av
/// tolkningen (se <see cref="ImportStepType"/>), så kjeden sorterer ikke om.
///
/// LIGGER I CORE, IKKE I WORKERS. Planen plasserer kjøringen i
/// <c>ImportBatchProcessor</c> i Workers, men det som står her er ren tolkning uten
/// infrastruktur — og enhetstestprosjektet ser ikke Workers. Workers eier batching,
/// flush og tilstand; denne klassen eier én post.
///
/// ANNOTASJONER OG HASH-SJEKK ER IKKE MED ENNÅ. Artskart 2 gjør begge inne i samme
/// metode: annotasjoner før stegene (fase 10) og «hopp over uendret post» på hash
/// pluss motor-ID (fase 9). Kroken hører hit når de fasene bygges.
/// </summary>
public class ImportRecordProcessor
{
    private readonly IImportStep[] _preSteps;
    private readonly IImportStep[] _parallelSteps;
    private readonly IImportStep[] _postSteps;
    private readonly IImportIdentityStep? _identityStep;

    public ImportRecordProcessor(IEnumerable<IImportStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var alle = steps.ToArray();

        _preSteps = alle.Where(x => x.StepType == ImportStepType.Pre).ToArray();
        _parallelSteps = alle.Where(x => x.StepType == ImportStepType.Parallel).ToArray();
        _postSteps = alle.Where(x => x.StepType == ImportStepType.Post).ToArray();
        _identityStep = alle.OfType<IImportIdentityStep>().FirstOrDefault();
    }

    public ImportRecordOutcome Process(CacheRecord record, ImportRunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(settings);

        var observation = new ImportedObservation
        {
            NodeId = settings.NodeId,
            ProcessEngineId = settings.ProcessEngineId,
            DateTimeRecordProcessed = settings.ProcessedAt,
            DateTimeRecordImported = settings.ImportedAt,
            DateLastModified = record.DateLastModified,
            HashCode = record.HashCode,
            HasErrors = false
        };

        var context = new ImportContext { IsDatasetSensitive = settings.IsDatasetSensitive };

        if (record.IsDeleted)
        {
            return ProcessDeleted(record, observation, context);
        }

        var warnings = new List<ImportRecordResult>();
        var errors = RunChain(record, observation, context, warnings);

        if (errors.Count > 0)
        {
            return Build(ImportRecordStatus.Rejected, observation, errors, warnings);
        }

        // Først her er alle steg ferdige, og de dynamiske egenskapene kan skrives.
        // Sorteringen på nøkkel er Artskart 2s, og den gjør JSON-en sammenlignbar
        // mellom to kjøringer.
        if (context.DynamicProperties.Count > 0)
        {
            var sortert = context.DynamicProperties
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value);

            observation.DynamicProperties = JsonSerializer.Serialize(sortert);
        }

        return Build(ImportRecordStatus.Imported, observation, errors, warnings);
    }

    /// <summary>
    /// En slettet post skal bare identifiseres, ikke tolkes. Uten identitet finnes det
    /// ingenting å slette, og Artskart 2 hopper da stille over posten.
    /// </summary>
    private ImportRecordOutcome ProcessDeleted(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        _identityStep?.ValidateAndImport(record, observation, context);

        var status = string.IsNullOrWhiteSpace(observation.ProxyId)
            ? ImportRecordStatus.DeletedWithoutIdentity
            : ImportRecordStatus.Deleted;

        return Build(status, observation, [], []);
    }

    /// <summary>
    /// Kjører de tre fasene og gir avvisningene tilbake. Tom liste betyr at posten gikk
    /// gjennom; advarslene samles i <paramref name="warnings"/> underveis.
    /// </summary>
    private List<ImportRecordResult> RunChain(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        List<ImportRecordResult> warnings)
    {
        var avvisninger = RunSequential(_preSteps, record, observation, context, warnings);
        if (avvisninger.Count > 0) return avvisninger;

        avvisninger = RunParallel(record, observation, context, warnings);
        if (avvisninger.Count > 0) return avvisninger;

        return RunSequential(_postSteps, record, observation, context, warnings);
    }

    private static List<ImportRecordResult> RunSequential(
        IImportStep[] steps,
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        List<ImportRecordResult> warnings)
    {
        foreach (var step in steps)
        {
            var resultat = step.ValidateAndImport(record, observation, context);

            if (resultat.IsWarning) warnings.Add(resultat);
            if (resultat.IsAccepted) continue;

            return [resultat];
        }

        return [];
    }

    /// <summary>
    /// Alle steg i den parallelle fasen kjører ferdig før posten eventuelt avvises.
    /// Det er forskjellen fra de sekvensielle fasene: de deler observasjonen, og et
    /// steg som stoppes halvveis etterlater en halvskrevet post.
    ///
    /// KJØRES SEKVENSIELT, IKKE SAMTIDIG — og det er en bevisst retting.
    /// Artskart 2 har to tasks av denne typen, <c>ImportGeometry</c> og
    /// <c>ImportMedia</c>, og starter dem med <c>Task.Run</c> + <c>WaitAll</c> på samme
    /// <c>Observation</c>. Begge legger til i <c>ObservationTags</c> (geometri
    /// <c>QualityIssue</c>, media <c>HasImage</c>), begge skriver til
    /// <c>AssociatedReferences</c>, og begge kan skrive til <c>Notes</c>. To tråder som
    /// legger til i samme liste er ikke trygt: en tagg kan forsvinne, og i verste fall
    /// kaster listen. Gevinsten er null — geometristeget er det tunge, og to oppgaver
    /// gir ingen parallellitet verdt navnet.
    ///
    /// Rekkefølgen er stegenes listerekkefølge, altså den samme som i Artskart 2s
    /// <c>CreateImportServiceTasks</c>: geometri før media.
    /// </summary>
    private List<ImportRecordResult> RunParallel(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        List<ImportRecordResult> warnings)
    {
        if (_parallelSteps.Length == 0) return [];

        var resultater = new List<ImportRecordResult>(_parallelSteps.Length);

        foreach (var step in _parallelSteps)
        {
            resultater.Add(step.ValidateAndImport(record, observation, context));
        }

        warnings.AddRange(resultater.Where(x => x.IsWarning));

        return resultater.Where(x => !x.IsAccepted).ToList();
    }

    private static ImportRecordOutcome Build(
        ImportRecordStatus status,
        ImportedObservation observation,
        List<ImportRecordResult> errors,
        List<ImportRecordResult> warnings)
    {
        var utfall = new ImportRecordOutcome
        {
            Status = status,
            Observation = observation
        };

        utfall.Errors.AddRange(errors);
        utfall.Warnings.AddRange(warnings);

        return utfall;
    }
}

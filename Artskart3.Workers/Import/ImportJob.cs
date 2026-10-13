namespace Artskart3.Workers.Import;

/// <summary>
/// Hangfire continuation/recurring job — entry point for import.
/// Leser fra cache-databasen, prosesserer og skriver til index-databasen.
/// TODO: Implementeres i neste fase.
///
/// VIKTIG — ObservationEntityIndex har en columnstore-indeks (IX_OEI_Columnstore).
/// Det legger noen føringer på hvordan daglige batcher må skrives:
///
/// - Bruk bulk-insert med minst 102 400 rader per setning. Da går radene rett inn i
///   en komprimert rowgroup. Mindre batcher havner i delta store (en vanlig b-tre)
///   og venter til ~1M rader har samlet seg før de komprimeres.
/// - Foretrekk append fremfor UPDATE. En UPDATE mot columnstore markerer den gamle
///   raden i delete-bitmappen og setter inn en ny versjon i delta store — den gamle
///   raden opptar fortsatt plass til neste rebuild. Skal eksisterende observasjoner
///   revideres, er bulk delete + insert billigere enn UPDATE-in-place.
/// - Kjør DatabaseMaintenanceJob (REORGANIZE) etter importen, se den klassen.
///
/// MERK: ObservationTaxonHierarchy har i dag ingen oppdateringssti — den populeres
/// kun én gang i migrasjon 20260702113806. Den må også vedlikeholdes herfra,
/// ellers blir taksonfiltrering stående på et frosset øyeblikksbilde.
/// </summary>
public class ImportJob
{
    private readonly ImportService _importService;
    private readonly ILogger<ImportJob> _logger;

    public ImportJob(ImportService importService, ILogger<ImportJob> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ImportJob er ikke implementert ennå");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Leser én node fra cachen og logger hva som finnes der. Skriver ingenting.
    ///
    /// Egen inngang, og ikke en del av <see cref="ExecuteAsync"/>, fordi den hører til
    /// verifiseringen av lesestien: den kjøres manuelt mot en node (enqueues fra
    /// Hangfire-dashbordet eller kalles direkte), mens <see cref="ExecuteAsync"/> blir
    /// den ordinære importen når den finnes. Når importen er på plass, er denne
    /// fortsatt den billigste måten å se om cachen svarer som forventet.
    /// </summary>
    public async Task ReadNodeAsync(int nodeId, int? maxRecords, CancellationToken cancellationToken)
    {
        await _importService.ReadNodeAsync(nodeId, maxRecords, cancellationToken);
    }
}

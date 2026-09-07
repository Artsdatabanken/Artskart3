namespace Artskart3.Workers.Maintenance;

/// <summary>
/// Hangfire recurring job som kjører før harvest for å sikre at referansedata er oppdatert.
///
/// Ansvarsområder:
/// - Taksonomi-sync: Oppdaterer taksontabellene fra Artsdatabankens taksonomi-API
///   (erstatter TaxonomyWash som i dag kjører i artskart2)
/// - Områdeoppdateringer: Oppdaterer kommune-/fylkesgrenser og verneområder
/// - Vedlikeholdsoppgaver: Datarydding og andre planlagte vedlikeholdsjobber
/// - Columnstore-vedlikehold: REORGANIZE av IX_OEI_Columnstore etter ImportJob
///
/// TODO: Implementeres i neste fase.
///
/// Columnstore-vedlikehold (kjøres etter ImportJob, ikke før):
///
///     ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex
///         REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON);
///
/// REORGANIZE er online og inkrementell — den slår sammen små rowgroups og fjerner
/// slettede rader fysisk. COMPRESS_ALL_ROW_GROUPS tvinger også gjenværende
/// delta-rowgroups til å komprimeres, noe som er nødvendig når en daglig batch
/// var mindre enn 102 400 rader.
///
/// Bruk REORGANIZE, ikke REBUILD. En full rebuild av 192M rader tar 10-30 minutter
/// og hører ikke hjemme i en daglig jobb. Rebuild er kun nødvendig når andelen
/// slettede rader passerer ~20 %, som kan overvåkes med:
///
///     SELECT SUM(total_rows), SUM(deleted_rows)
///     FROM sys.dm_db_column_store_row_group_physical_stats
///     WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex');
/// </summary>
public class DatabaseMaintenanceJob
{
    private readonly ILogger<DatabaseMaintenanceJob> _logger;

    public DatabaseMaintenanceJob(ILogger<DatabaseMaintenanceJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabaseMaintenanceJob er ikke implementert ennå");
        return Task.CompletedTask;
    }
}

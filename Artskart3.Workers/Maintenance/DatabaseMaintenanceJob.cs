namespace Artskart3.Workers.Maintenance;

/// <summary>
/// Hangfire recurring job som kjører før harvest for å sikre at referansedata er oppdatert.
///
/// Ansvarsområder:
/// - Taksonomi-sync: Oppdaterer taksontabellene fra Artsdatabankens taksonomi-API
///   (erstatter TaxonomyWash som i dag kjører i artskart2)
/// - Områdeoppdateringer: Oppdaterer kommune-/fylkesgrenser og verneområder
/// - Vedlikeholdsoppgaver: Datarydding og andre planlagte vedlikeholdsjobber
///
/// TODO: Implementeres i neste fase. 
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

using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Workers.Configuration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

/// <summary>
/// Hangfire-jobb som poller databasen for ventende CSV-eksportjobber og prosesserer dem.
/// Gjenoppretter også jobber som har hengt i Processing-status for lenge (f.eks. etter krasj).
/// </summary>
public class CsvExportPollJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsvExportPollJob> _logger;
    private readonly CsvExportOptions _options;

    public CsvExportPollJob(IServiceScopeFactory scopeFactory, ILogger<CsvExportPollJob> logger, IOptions<CsvExportOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        await RecoverStuckJobsAsync(context, cancellationToken);

        // Atomisk claim: marker eldste ventende jobb som Processing i én operasjon
        var now = DateTime.UtcNow;
        var oldestPendingId = await context.Set<CsvExportJob>()
            .Where(j => j.Status == CsvExportStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Select(j => (int?)j.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestPendingId == null)
            return;

        var claimed = await context.Set<CsvExportJob>()
            .Where(j => j.Id == oldestPendingId.Value && j.Status == CsvExportStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Processing)
                .SetProperty(j => j.StartedAt, now),
                cancellationToken);

        if (claimed == 0)
            return; // En annen worker tok jobben først

        var job = await context.Set<CsvExportJob>()
            .FirstAsync(j => j.Id == oldestPendingId.Value, cancellationToken);

        _logger.LogInformation("Starter eksportjobb {JobId} for bruker {UserId}", job.Id, job.UserId);

        try
        {
            var exportService = scope.ServiceProvider.GetRequiredService<ExportService>();
            await exportService.ProcessJobAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Eksportjobb {JobId} ble avbrutt (shutdown)", job.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eksportjobb {JobId} feilet", job.Id);

            // Bruk ExecuteUpdate for å unngå stale entity-problemer
            await context.Set<CsvExportJob>()
                .Where(j => j.Id == job.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, CsvExportStatus.Failed)
                    .SetProperty(j => j.ErrorMessage,
                        ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message)
                    .SetProperty(j => j.CompletedAt, DateTime.UtcNow),
                    CancellationToken.None);
        }
    }

    /// <summary>
    /// Setter hengende Processing-jobber tilbake til Pending — men gir opp etter
    /// MaxAttempts forsøk.
    ///
    /// Oppgivelsen er poenget. En jobb som DREPER prosessen (OOM, uventet exit)
    /// rekker aldri å bli merket Failed av catch-blokken over. Den ble bare satt
    /// tilbake til Pending med sin opprinnelige CreatedAt i behold, og siden
    /// claimen plukker eldste ventende jobb var den straks først i køen igjen.
    /// Resultatet var en evighetsløkke som samtidig sultet ut alle andres
    /// eksporter — ingenting bak den i køen kom noen gang gjennom.
    ///
    /// Offentlig fordi gjenopprettingen er et eget ansvar som må kunne testes for
    /// seg. Kjøres den via ExecuteAsync, blir jobben claimet og prosessert i det
    /// samme kallet, og da er det ikke lenger gjenopprettingen man observerer.
    /// </summary>
    /// <returns>Antall jobber satt tilbake til Pending, og antall gitt opp.</returns>
    public async Task<(int Recovered, int Abandoned)> RecoverStuckJobsAsync(
        IArtsKartDbContext context,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(_options.Worker.StuckJobTimeoutMinutes);
        var cutoff = DateTime.UtcNow - timeout;
        var maxAttempts = _options.Worker.MaxAttempts;

        // Rekkefølgen er viktig: de som har brukt opp forsøkene sine må merkes
        // Failed FØR resten settes tilbake, ellers får de nettopp det ekstra
        // forsøket denne sjekken skulle hindre.
        var abandonedCount = await context.Set<CsvExportJob>()
            .Where(j => j.Status == CsvExportStatus.Processing
                     && j.StartedAt < cutoff
                     && j.Attempts + 1 >= maxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Failed)
                .SetProperty(j => j.Attempts, j => j.Attempts + 1)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow)
                .SetProperty(j => j.ErrorMessage,
                    $"Eksporten ble avbrutt {maxAttempts} ganger uten å fullføre, og er gitt opp."),
                cancellationToken);

        var recoveredCount = await context.Set<CsvExportJob>()
            .Where(j => j.Status == CsvExportStatus.Processing && j.StartedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Pending)
                .SetProperty(j => j.Attempts, j => j.Attempts + 1)
                .SetProperty(j => j.StartedAt, (DateTime?)null)
                .SetProperty(j => j.RowsProcessed, 0),
                cancellationToken);

        if (recoveredCount > 0)
        {
            _logger.LogWarning("Tilbakestilte {Count} eksportjobber som hadde hengt i Processing", recoveredCount);
        }

        if (abandonedCount > 0)
        {
            _logger.LogError(
                "Ga opp {Count} eksportjobber etter {MaxAttempts} mislykkede forsøk",
                abandonedCount, maxAttempts);
        }

        return (recoveredCount, abandonedCount);
    }
}

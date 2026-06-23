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
            var exportService = scope.ServiceProvider.GetRequiredService<CsvExportService>();
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

    private async Task RecoverStuckJobsAsync(IArtsKartDbContext context, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(_options.Worker.StuckJobTimeoutMinutes);
        var cutoff = DateTime.UtcNow - timeout;

        var stuckCount = await context.Set<CsvExportJob>()
            .Where(j => j.Status == CsvExportStatus.Processing && j.StartedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Pending)
                .SetProperty(j => j.StartedAt, (DateTime?)null)
                .SetProperty(j => j.RowsProcessed, 0),
                cancellationToken);

        if (stuckCount > 0)
        {
            _logger.LogWarning("Tilbakestilte {Count} eksportjobber som hadde hengt i Processing", stuckCount);
        }
    }
}

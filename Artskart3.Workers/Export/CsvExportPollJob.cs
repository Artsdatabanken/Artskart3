using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Workers.Export;

/// <summary>
/// Hangfire-jobb som poller databasen for ventende CSV-eksportjobber og prosesserer dem.
/// </summary>
public class CsvExportPollJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsvExportPollJob> _logger;

    public CsvExportPollJob(IServiceScopeFactory scopeFactory, ILogger<CsvExportPollJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        // Hent eldste ventende jobb
        var pendingJob = await context.Set<CsvExportJob>()
            .Where(j => j.Status == CsvExportStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingJob == null)
            return;

        _logger.LogInformation("Starter eksportjobb {JobId} for bruker {UserId}", pendingJob.Id, pendingJob.UserId);

        // Marker som under behandling
        pendingJob.Status = CsvExportStatus.Processing;
        pendingJob.StartedAt = DateTime.UtcNow;
        context.Set<CsvExportJob>().Update(pendingJob);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var exportService = scope.ServiceProvider.GetRequiredService<CsvExportService>();
            await exportService.ProcessJobAsync(pendingJob, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Eksportjobb {JobId} ble avbrutt (shutdown)", pendingJob.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eksportjobb {JobId} feilet", pendingJob.Id);

            pendingJob.Status = CsvExportStatus.Failed;
            pendingJob.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            pendingJob.CompletedAt = DateTime.UtcNow;
            context.Set<CsvExportJob>().Update(pendingJob);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}

using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Workers.Export;

/// <summary>
/// Hangfire-jobb som sletter utløpte eksportjobber og tilhørende blob-filer.
/// </summary>
public class CsvExportCleanupJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsvExportCleanupJob> _logger;

    public CsvExportCleanupJob(IServiceScopeFactory scopeFactory, ILogger<CsvExportCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        var now = DateTime.UtcNow;
        var oneDayAgo = now.AddDays(-1);

        // Hent jobber som skal ryddes opp:
        // 1. Fullførte jobber der ExpiresAt er passert
        // 2. Feilede/kansellerte jobber med blob-referanser eldre enn 1 dag
        var expiredJobs = await context.Set<CsvExportJob>()
            .Where(j =>
                (j.Status == CsvExportStatus.Complete && j.ExpiresAt != null && j.ExpiresAt < now) ||
                ((j.Status == CsvExportStatus.Failed || j.Status == CsvExportStatus.Cancelled)
                    && (j.BlobPath != null || j.ExcelBlobPath != null)
                    && j.CompletedAt != null && j.CompletedAt < oneDayAgo))
            .ToListAsync(cancellationToken);

        if (expiredJobs.Count == 0)
            return;

        _logger.LogInformation("Starter opprydding av {Count} utløpte eksportjobber", expiredJobs.Count);

        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrEmpty(job.BlobPath))
            {
                try
                {
                    await blobStorage.DeleteBlobAsync(job.BlobPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kunne ikke slette CSV-blob {BlobPath} for jobb {JobId}", job.BlobPath, job.Id);
                }
            }

            if (!string.IsNullOrEmpty(job.ExcelBlobPath))
            {
                try
                {
                    await blobStorage.DeleteBlobAsync(job.ExcelBlobPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kunne ikke slette Excel-blob {BlobPath} for jobb {JobId}", job.ExcelBlobPath, job.Id);
                }
            }
        }

        // Fjern blob-referanser og marker jobbene som utløpte
        var expiredIds = expiredJobs.Select(j => j.Id).ToList();
        await context.Set<CsvExportJob>()
            .Where(j => expiredIds.Contains(j.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.BlobPath, (string?)null)
                .SetProperty(j => j.ExcelBlobPath, (string?)null)
                .SetProperty(j => j.FileSize, 0L),
                cancellationToken);

        _logger.LogInformation("Opprydding fullført. {Count} utløpte jobber behandlet", expiredJobs.Count);
    }
}

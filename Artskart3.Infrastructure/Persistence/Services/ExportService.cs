using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// API-side implementasjon av IExportService.
/// Håndterer opprettelse og administrasjon av eksportjobber i databasen.
/// Selve CSV-genereringen gjøres av workeren.
/// </summary>
public class ExportService : IExportService
{
    private readonly IArtsKartDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ExportColumnRegistry _columnRegistry;

    public ExportService(IArtsKartDbContext context, IConfiguration configuration, ExportColumnRegistry columnRegistry)
    {
        _context = context;
        _configuration = configuration;
        _columnRegistry = columnRegistry;
    }

    public Task<IReadOnlyList<ExportColumnDefinition>> GetAvailableColumnsAsync()
    {
        return Task.FromResult(_columnRegistry.GetAllColumns());
    }

    public async Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(filter);
        var count = await query.CountAsync(cancellationToken);

        var softLimit = _configuration.GetValue("CsvExport:Limits:SoftRowLimit", 50_000);
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 100_000);

        // Estimert filstørrelse: ~500 bytes per rad skalert etter andel valgte kolonner,
        // pluss CSV-overhead (skilletegn, linjeskift, header-rad og sep-hint)
        var totalColumns = _columnRegistry.GetAllColumns().Count;
        var selectedCount = columns.Count > 0 ? columns.Count : _columnRegistry.GetDefaultColumnNames().Count;
        var columnRatio = (double)selectedCount / totalColumns;
        var csvOverheadPerRow = selectedCount + 1; // semikolon mellom kolonner + \r\n
        var estimatedSize = (long)(count * (500 * columnRatio + csvOverheadPerRow));

        return new ExportSummaryDto
        {
            TotalRows = count,
            EstimatedFileSizeBytes = estimatedSize,
            ExceedsSoftLimit = count > softLimit,
            ExceedsHardLimit = count > hardLimit,
            ExportName = SanitizeExportName(name),
            HardLimit = hardLimit
        };
    }

    private static string SanitizeExportName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "eksport";

        var sanitized = new string(name
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        return string.IsNullOrEmpty(sanitized) ? "eksport" : sanitized;
    }

    public async Task<int> StartExportAsync(string userId, ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken)
    {
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 100_000);
        var maxConcurrent = _configuration.GetValue("CsvExport:Limits:MaxConcurrentPerUser", 3);

        // Sjekk antall samtidige jobber for bruker.
        // Merk: dette er en TOCTOU-sjekk (les-så-skriv uten serializable transaksjon),
        // men med forventet lavt volum er race condition ikke et reelt problem.
        // Revurder hvis volumet øker vesentlig.
        var activeJobCount = await _context.Set<CsvExportJob>()
            .CountAsync(j => j.UserId == userId &&
                (j.Status == CsvExportStatus.Pending || j.Status == CsvExportStatus.Processing), cancellationToken);

        if (activeJobCount >= maxConcurrent)
            throw new InvalidOperationException(
                $"Maks {maxConcurrent} samtidige eksportjobber per bruker.");

        // Sjekk hard limit uten full COUNT — stopp tidlig hvis rad N+1 finnes
        var query = BuildFilteredQuery(filter);
        var exceedsLimit = await query.OrderBy(o => o.Id).Skip(hardLimit).AnyAsync(cancellationToken);

        if (exceedsLimit)
            throw new InvalidOperationException(
                $"Antall rader overstiger grensen ({hardLimit}). Bruk summary-endepunktet for nøyaktig antall.");

        var job = new CsvExportJob
        {
            UserId = userId,
            Name = name?.Trim(),
            Status = CsvExportStatus.Pending,
            FilterJson = JsonSerializer.Serialize(filter),
            SelectedColumns = JsonSerializer.Serialize(columns),
        };

        _context.Set<CsvExportJob>().Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    public async Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, string userId, CancellationToken cancellationToken)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        return job == null ? null : MapToDto(job);
    }

    public async Task<bool> CancelExportAsync(int jobId, string userId, CancellationToken cancellationToken)
    {
        // Atomisk oppdatering — unngår at workeren overskriver Complete med Cancelled
        var updated = await _context.Set<CsvExportJob>()
            .Where(j => j.Id == jobId && j.UserId == userId &&
                   (j.Status == CsvExportStatus.Pending || j.Status == CsvExportStatus.Processing))
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Cancelled)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow),
                cancellationToken);

        return updated > 0;
    }

    public async Task<string?> GetCsvBlobPathAsync(int jobId, string userId, CancellationToken cancellationToken)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job?.Status != CsvExportStatus.Complete || string.IsNullOrEmpty(job.BlobPath))
            return null;

        if (job.ExpiresAt.HasValue && job.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        return job.BlobPath;
    }

    public async Task<string?> GetExcelBlobPathAsync(int jobId, string userId, CancellationToken cancellationToken)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job?.Status != CsvExportStatus.Complete || string.IsNullOrEmpty(job.ExcelBlobPath))
            return null;

        if (job.ExpiresAt.HasValue && job.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        return job.ExcelBlobPath;
    }

    public async Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(string userId, CancellationToken cancellationToken)
    {
        var jobs = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return jobs.Select(MapToDto).ToList();
    }

    private IQueryable<Observation> BuildFilteredQuery(ObservationSearchFilterDto filter)
    {
        var query = _context.Set<Observation>().AsNoTracking();
        return ObservationQueryBuilder.ApplyFilters(_context, query, filter);
    }

    private static CsvExportJobDto MapToDto(CsvExportJob job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        FileName = $"{job.Id}-{SanitizeExportName(job.Name)}",
        Status = job.Status,
        TotalRows = job.TotalRows,
        RowsProcessed = job.RowsProcessed,
        FileSize = job.FileSize,
        HasExcel = !string.IsNullOrEmpty(job.ExcelBlobPath),
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        ExpiresAt = job.ExpiresAt,
        ErrorMessage = job.ErrorMessage
    };
}

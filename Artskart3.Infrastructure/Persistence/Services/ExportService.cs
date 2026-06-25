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

    public async Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns)
    {
        var query = BuildFilteredQuery(filter);
        var count = await query.CountAsync();

        var softLimit = _configuration.GetValue("CsvExport:Limits:SoftRowLimit", 50_000);
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 100_000);

        // Estimert filstørrelse: ~200 bytes per rad * antall valgte kolonner / totalt mulige kolonner
        var columnRatio = Math.Max(columns.Count, 1) / 50.0;
        var estimatedSize = (long)(count * 200 * columnRatio);

        return new ExportSummaryDto
        {
            TotalRows = count,
            EstimatedFileSizeBytes = estimatedSize,
            ExceedsSoftLimit = count > softLimit,
            ExceedsHardLimit = count > hardLimit
        };
    }

    public async Task<int> StartExportAsync(string userId, ObservationSearchFilterDto filter, List<string> columns, string? name)
    {
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 100_000);
        var maxConcurrent = _configuration.GetValue("CsvExport:Limits:MaxConcurrentPerUser", 3);

        // Sjekk antall samtidige jobber for bruker
        var activeJobCount = await _context.Set<CsvExportJob>()
            .CountAsync(j => j.UserId == userId &&
                (j.Status == CsvExportStatus.Pending || j.Status == CsvExportStatus.Processing));

        if (activeJobCount >= maxConcurrent)
            throw new InvalidOperationException(
                $"Maks {maxConcurrent} samtidige eksportjobber per bruker.");

        // Sjekk hard limit uten full COUNT — stopp tidlig hvis rad N+1 finnes
        var query = BuildFilteredQuery(filter);
        var exceedsLimit = await query.Skip(hardLimit).AnyAsync();

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
        await _context.SaveChangesAsync();

        return job.Id;
    }

    public async Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, string userId)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

        return job == null ? null : MapToDto(job);
    }

    public async Task<bool> CancelExportAsync(int jobId, string userId)
    {
        var job = await _context.Set<CsvExportJob>()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

        if (job == null)
            return false;

        if (job.Status != CsvExportStatus.Pending && job.Status != CsvExportStatus.Processing)
            return false;

        job.Status = CsvExportStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<string?> GetCsvBlobPathAsync(int jobId, string userId)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

        if (job?.Status != CsvExportStatus.Complete || string.IsNullOrEmpty(job.BlobPath))
            return null;

        return job.BlobPath;
    }

    public async Task<string?> GetExcelBlobPathAsync(int jobId, string userId)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

        if (job?.Status != CsvExportStatus.Complete || string.IsNullOrEmpty(job.ExcelBlobPath))
            return null;

        return job.ExcelBlobPath;
    }

    public async Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(string userId)
    {
        var jobs = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync();

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

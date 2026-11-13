using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly ITaxonHierarchyService _taxonHierarchy;
    private readonly ICadastralUnitService _cadastralUnits;

    public ExportService(
        IArtsKartDbContext context,
        IConfiguration configuration,
        ExportColumnRegistry columnRegistry,
        ITaxonHierarchyService taxonHierarchy,
        ICadastralUnitService cadastralUnits)
    {
        _context = context;
        _configuration = configuration;
        _columnRegistry = columnRegistry;
        _taxonHierarchy = taxonHierarchy;
        _cadastralUnits = cadastralUnits;
    }

    public Task<IReadOnlyList<ExportColumnDefinition>> GetAvailableColumnsAsync()
    {
        return Task.FromResult(_columnRegistry.GetAllColumns());
    }

    public async Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken)
    {
        var query = await BuildFilteredQueryAsync(filter, cancellationToken);
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

    public async Task<int> StartExportAsync(Guid userId, bool isAnonymous, ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken)
    {
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 100_000);
        var maxConcurrent = isAnonymous
            ? _configuration.GetValue("CsvExport:Limits:MaxConcurrentAnonymousPerUser", 1)
            : _configuration.GetValue("CsvExport:Limits:MaxConcurrentPerUser", 3);

        // Sjekk tidlig for å unngå den kostbare filterspørringen når grensen allerede
        // er nådd. Antall sjekkes på nytt under en database-lås før jobben lagres.
        var activeJobCutoffHours = _configuration.GetValue("CsvExport:Limits:ActiveJobCutoffHours", 24);
        var activeJobCutoff = DateTime.UtcNow.AddHours(-activeJobCutoffHours);

        var activeJobCount = await CountActiveJobsAsync(userId, activeJobCutoff, cancellationToken);

        if (activeJobCount >= maxConcurrent)
            throw CreateConcurrencyLimitException(maxConcurrent, isAnonymous);

        // Sjekk hard limit uten full COUNT — stopp tidlig hvis rad N+1 finnes
        var query = await BuildFilteredQueryAsync(filter, cancellationToken);
        var exceedsLimit = await query.OrderBy(o => o.Id).Skip(hardLimit).AnyAsync(cancellationToken);

        if (exceedsLimit)
            throw new InvalidOperationException(
                $"Antall rader overstiger grensen ({hardLimit}). Bruk summary-endepunktet for nøyaktig antall.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await AcquireExportStartLockAsync(userId, cancellationToken);

        activeJobCount = await CountActiveJobsAsync(userId, activeJobCutoff, cancellationToken);
        if (activeJobCount >= maxConcurrent)
            throw CreateConcurrencyLimitException(maxConcurrent, isAnonymous);

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
        await transaction.CommitAsync(cancellationToken);

        return job.Id;
    }

    private Task<int> CountActiveJobsAsync(Guid userId, DateTime cutoff, CancellationToken cancellationToken)
    {
        return _context.Set<CsvExportJob>()
            .CountAsync(j => j.UserId == userId &&
                j.CreatedAt >= cutoff &&
                (j.Status == CsvExportStatus.Pending || j.Status == CsvExportStatus.Processing), cancellationToken);
    }

    private static InvalidOperationException CreateConcurrencyLimitException(int maxConcurrent, bool isAnonymous)
    {
        var userType = isAnonymous ? "anonym bruker" : "bruker";
        return new InvalidOperationException(
            $"Maks {maxConcurrent} samtidige eksportjobber per {userType}. " +
            "Vent til en av dem er ferdig, eller avbryt en av dem i Mitt Artskart.");
    }

    private async Task AcquireExportStartLockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 5000;
            SELECT @result;
            """;

        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.Value = $"CsvExport:{userId:N}";
        command.Parameters.Add(resource);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new InvalidOperationException("En annen eksportstart pågår. Prøv igjen.");
    }

    public async Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, Guid userId, CancellationToken cancellationToken)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        return job == null ? null : MapToDto(job);
    }

    public async Task<bool> CancelExportAsync(int jobId, Guid userId, CancellationToken cancellationToken)
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

    public async Task<string?> GetCsvBlobPathAsync(int jobId, Guid userId, CancellationToken cancellationToken)
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

    public async Task<string?> GetExcelBlobPathAsync(int jobId, Guid userId, CancellationToken cancellationToken)
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

    public async Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var jobs = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return jobs.Select(MapToDto).ToList();
    }

    public async Task<int> ClaimAnonymousExportsAsync(Guid userId, Guid anonymousUserId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || anonymousUserId == Guid.Empty)
            throw new ArgumentException("Export owner IDs must be valid GUIDs.");

        if (userId == anonymousUserId)
            return 0;

        return await _context.Set<CsvExportJob>()
            .Where(j => j.UserId == anonymousUserId)
            .ExecuteUpdateAsync(update => update.SetProperty(j => j.UserId, userId), cancellationToken);
    }

    private Task<IQueryable<Observation>> BuildFilteredQueryAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken)
    {
        var query = _context.Set<Observation>().AsNoTracking();
        return ObservationQueryBuilder.ApplyFiltersAsync(_context, _taxonHierarchy, _cadastralUnits, query, filter, cancellationToken);
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

using System.Text;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Artskart3.Workers.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

/// <summary>
/// Kjører selve CSV-eksporten: leser observasjoner i batcher og streamer til blob storage.
/// </summary>
public class CsvExportService
{
    private readonly IArtsKartDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly CsvWriterService _csvWriter;
    private readonly ExportColumnRegistry _columnRegistry;
    private readonly CsvExportOptions _options;
    private readonly ILogger<CsvExportService> _logger;

    public CsvExportService(
        IArtsKartDbContext context,
        IBlobStorageService blobStorage,
        CsvWriterService csvWriter,
        ExportColumnRegistry columnRegistry,
        IOptions<CsvExportOptions> options,
        ILogger<CsvExportService> logger)
    {
        _context = context;
        _blobStorage = blobStorage;
        _csvWriter = csvWriter;
        _columnRegistry = columnRegistry;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessJobAsync(CsvExportJob job, CancellationToken cancellationToken)
    {
        var columns = JsonSerializer.Deserialize<List<string>>(job.SelectedColumns) ?? [];
        var filter = JsonSerializer.Deserialize<ObservationSearchFilterDto>(job.FilterJson);

        var validColumns = _columnRegistry.GetValidColumnNames();
        columns = columns.Where(c => validColumns.Contains(c)).ToList();

        if (columns.Count == 0)
        {
            columns = _columnRegistry.GetDefaultColumnNames();
        }

        var needsDetail = columns.Any(c => c.StartsWith("Detail."));

        // Bygg query med delt filterlogikk
        var query = BuildQuery(filter, needsDetail);

        // Tell rader og avbryt om det overstiger grensen
        var rowCount = await query.CountAsync(cancellationToken);
        var hardLimit = _options.Limits.HardRowLimit;

        if (rowCount > hardLimit)
        {
            throw new InvalidOperationException(
                $"Antall observasjoner ({rowCount}) overstiger maks tillatt ({hardLimit}). Jobb {job.Id} avbrutt.");
        }

        job.TotalRows = rowCount;

        var blobPath = $"{job.UserId}/{job.Id}.csv";

        await using var blobStream = await _blobStorage.OpenWriteStreamAsync(blobPath, cancellationToken);
        await using var writer = new StreamWriter(blobStream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true);

        // Skriv header
        var displayNames = columns.Select(c =>
            _columnRegistry.GetAllColumns().FirstOrDefault(col => col.Name == c)?.DisplayName ?? c).ToList();
        await _csvWriter.WriteHeaderAsync(writer, displayNames);

        var batchSize = _options.Worker.BatchSize;
        var delayMs = _options.Worker.InterBatchDelayMs;
        var totalProcessed = 0;
        var lastId = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Sjekk om jobben er kansellert
            var currentStatus = await _context.Set<CsvExportJob>()
                .Where(j => j.Id == job.Id)
                .Select(j => j.Status)
                .FirstAsync(cancellationToken);

            if (currentStatus == CsvExportStatus.Cancelled)
            {
                _logger.LogInformation("Eksportjobb {JobId} ble kansellert", job.Id);
                await writer.FlushAsync(cancellationToken);
                await _blobStorage.DeleteBlobAsync(blobPath, cancellationToken);
                return;
            }

            // Hent neste batch med keyset-paginering
            var batch = await query
                .Where(o => o.Id > lastId)
                .OrderBy(o => o.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var observation in batch)
            {
                var values = columns.Select(c => ExportColumnRegistry.GetValue(observation, c)).ToList();
                await _csvWriter.WriteRowAsync(writer, values);
            }

            totalProcessed += batch.Count;
            lastId = batch[^1].Id;

            // Oppdater fremdrift
            job.RowsProcessed = totalProcessed;
            _context.Set<CsvExportJob>().Update(job);
            await _context.SaveChangesAsync(cancellationToken);

            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);

        // Oppdater jobben som ferdig
        job.RowsProcessed = totalProcessed;
        job.TotalRows = totalProcessed;
        job.BlobPath = blobPath;
        job.FileSize = blobStream.Position;
        job.Status = CsvExportStatus.Complete;
        job.CompletedAt = DateTime.UtcNow;

        _context.Set<CsvExportJob>().Update(job);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Eksportjobb {JobId} fullført. {Rows} rader eksportert", job.Id, totalProcessed);
    }

    private IQueryable<Observation> BuildQuery(ObservationSearchFilterDto? filter, bool includeDetail)
    {
        var query = _context.Set<Observation>().AsNoTracking();

        if (includeDetail)
        {
            query = query.Include(o => o.ObservationDetail);
        }

        if (filter != null)
        {
            query = ObservationQueryBuilder.ApplyFilters(_context, query, filter);
        }

        return query;
    }
}

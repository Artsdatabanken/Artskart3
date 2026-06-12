using System.Text;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Application.Services;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Artskart3.Workers.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

/// <summary>
/// Kjører selve CSV-eksporten: leser observasjoner i batcher og streamer til blob storage.
/// Genererer også en Excel-fil (.xlsx) fra CSV-en.
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
        var filter = JsonSerializer.Deserialize<ObservationSearchFilterDto>(job.FilterJson)
                     ?? new ObservationSearchFilterDto();

        var validColumns = _columnRegistry.GetValidColumnNames();
        columns = columns.Where(c => validColumns.Contains(c)).ToList();

        if (columns.Count == 0)
        {
            columns = _columnRegistry.GetDefaultColumnNames();
        }

        var needsDetail = columns.Any(c => c.StartsWith("Detail."));

        var query = BuildQuery(filter, needsDetail);

        var csvBlobPath = $"{job.UserId}/{job.Id}.csv";
        var excelBlobPath = $"{job.UserId}/{job.Id}.xlsx";

        // TODO: Bytt til OpenWriteStreamAsync (streaming direkte til blob) når Azurite-bug er fikset.
        // Bruker midlertidig MemoryStream + UploadAsync for å unngå ETag-feil i Azurite.
        using var memoryStream = new MemoryStream();
        await using var writer = new StreamWriter(memoryStream, new UTF8Encoding(true), bufferSize: 8192, leaveOpen: true);

        // Excel-hint for semikolon-skilletegn
        await writer.WriteLineAsync("sep=;");

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

        // Last opp CSV til blob storage
        await _blobStorage.UploadAsync(csvBlobPath, memoryStream, cancellationToken);

        // Generer og last opp Excel-fil
        try
        {
            await GenerateAndUploadExcelAsync(memoryStream, excelBlobPath, cancellationToken);
            job.ExcelBlobPath = excelBlobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunne ikke generere Excel for jobb {JobId}, CSV er tilgjengelig", job.Id);
        }

        // Oppdater jobben som ferdig
        job.RowsProcessed = totalProcessed;
        job.TotalRows = totalProcessed;
        job.BlobPath = csvBlobPath;
        job.FileSize = memoryStream.Length;
        job.Status = CsvExportStatus.Complete;
        job.CompletedAt = DateTime.UtcNow;

        _context.Set<CsvExportJob>().Update(job);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Eksportjobb {JobId} fullført. {Rows} rader eksportert", job.Id, totalProcessed);
    }

    private async Task GenerateAndUploadExcelAsync(MemoryStream csvStream, string excelBlobPath, CancellationToken cancellationToken)
    {
        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Observasjoner");

        var row = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            // Hopp over sep=; hint
            if (row == 1 && line.StartsWith("sep="))
                continue;

            var fields = line.Split(';');
            for (var col = 0; col < fields.Length; col++)
            {
                worksheet.Cell(row, col + 1).Value = UnescapeCsvField(fields[col]);
            }
            row++;
        }

        using var excelStream = new MemoryStream();
        workbook.SaveAs(excelStream);
        excelStream.Position = 0;
        await _blobStorage.UploadAsync(excelBlobPath, excelStream, cancellationToken);

        _logger.LogInformation("Excel-fil generert og lastet opp: {BlobPath}", excelBlobPath);
    }

    private static string UnescapeCsvField(string field)
    {
        if (field.Length >= 2 && field[0] == '"' && field[^1] == '"')
        {
            return field[1..^1].Replace("\"\"", "\"");
        }
        return field;
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

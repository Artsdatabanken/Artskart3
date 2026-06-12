using System.Globalization;
using System.Text;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Artskart3.Workers.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

/// <summary>
/// Kjører CSV- og Excel-eksport: leser observasjoner i batcher og bygger begge formatene samtidig.
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

        // Kolonne-visningsnavn (brukes av både CSV og Excel)
        var columnMap = _columnRegistry.GetAllColumns().ToDictionary(c => c.Name, c => c.DisplayName);
        var displayNames = columns.Select(c => columnMap.GetValueOrDefault(c, c)).ToList();

        // TODO: Bytt til OpenWriteStreamAsync (streaming direkte til blob) når Azurite-bug er fikset.
        using var csvStream = new MemoryStream();
        await using var writer = new StreamWriter(csvStream, new UTF8Encoding(true), bufferSize: 8192, leaveOpen: true);

        // CSV: sep-hint + header
        await writer.WriteLineAsync("sep=;");
        await _csvWriter.WriteHeaderAsync(writer, displayNames);

        // Excel: opprett arbeidsbok og skriv header
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Observasjoner");
        for (var col = 0; col < displayNames.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = displayNames[col];
        }

        var batchSize = _options.Worker.BatchSize;
        var delayMs = _options.Worker.InterBatchDelayMs;
        var totalProcessed = 0;
        var lastId = 0;
        var excelRow = 2; // Rad 1 er header

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

                // Skriv til CSV
                await _csvWriter.WriteRowAsync(writer, values);

                // Skriv til Excel
                for (var col = 0; col < values.Count; col++)
                {
                    worksheet.Cell(excelRow, col + 1).Value = FormatExcelValue(values[col]);
                }
                excelRow++;
            }

            totalProcessed += batch.Count;
            lastId = batch[^1].Id;

            // Oppdater fremdrift og rydd opp change tracker
            await _context.Set<CsvExportJob>()
                .Where(j => j.Id == job.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.RowsProcessed, totalProcessed),
                    cancellationToken);

            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);

        // Last opp CSV
        await _blobStorage.UploadAsync(csvBlobPath, csvStream, cancellationToken);

        // Last opp Excel
        string? savedExcelPath = null;
        try
        {
            using var excelStream = new MemoryStream();
            workbook.SaveAs(excelStream);
            excelStream.Position = 0;
            await _blobStorage.UploadAsync(excelBlobPath, excelStream, cancellationToken);
            savedExcelPath = excelBlobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunne ikke laste opp Excel for jobb {JobId}, CSV er tilgjengelig", job.Id);
        }

        // Oppdater jobben som ferdig
        await _context.Set<CsvExportJob>()
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.RowsProcessed, totalProcessed)
                .SetProperty(j => j.TotalRows, totalProcessed)
                .SetProperty(j => j.BlobPath, csvBlobPath)
                .SetProperty(j => j.ExcelBlobPath, savedExcelPath)
                .SetProperty(j => j.FileSize, csvStream.Length)
                .SetProperty(j => j.Status, CsvExportStatus.Complete)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow),
                cancellationToken);

        _logger.LogInformation("Eksportjobb {JobId} fullført. {Rows} rader eksportert", job.Id, totalProcessed);
    }

    private static XLCellValue FormatExcelValue(object? value)
    {
        return value switch
        {
            null => Blank.Value,
            string s => s,
            int i => i,
            long l => l,
            double d => d,
            float f => f,
            bool b => b,
            DateTime dt => dt,
            _ => value.ToString() ?? ""
        };
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

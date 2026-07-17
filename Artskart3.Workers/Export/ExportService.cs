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
public class ExportService
{
    private readonly IArtsKartDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly CsvWriterService _csvWriter;
    private readonly ExportColumnRegistry _columnRegistry;
    private readonly CsvExportOptions _options;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IArtsKartDbContext context,
        IBlobStorageService blobStorage,
        CsvWriterService csvWriter,
        ExportColumnRegistry columnRegistry,
        IOptions<CsvExportOptions> options,
        ILogger<ExportService> logger)
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
        await EnsureBlobStorageConnectionAsync(cancellationToken);

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

        // Tell totalt antall rader og lagre på jobben med én gang
        var totalRows = await query.CountAsync(cancellationToken);
        await _context.Set<CsvExportJob>()
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.TotalRows, totalRows),
                cancellationToken);

        var fileSlug = SanitizeBlobName(job.Name);
        var csvBlobPath = $"exports/{job.Id}-{fileSlug}.csv";
        var excelBlobPath = $"exports/{job.Id}-{fileSlug}.xlsx";

        // Kolonne-visningsnavn (brukes av både CSV og Excel)
        var columnMap = _columnRegistry.GetAllColumns().ToDictionary(c => c.Name, c => c.DisplayName);
        var displayNames = columns.Select(c => columnMap.GetValueOrDefault(c, c)).ToList();

        // OBS: Hele CSV-en og Excel-arbeidsboken bygges i minnet før opplasting.
        // Ved 500k rader kan dette bruke 1+ GB RAM (ClosedXML er spesielt tungt).
        // Når Azurite-bug er fikset: bruk BlobClient.OpenWriteAsync for å streame CSV
        // direkte til blob storage, og begrens Excel til maks ~50k rader (eller bruk MiniExcel).
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
        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(_options.Worker.ExpiresAtDays);
        await _context.Set<CsvExportJob>()
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.RowsProcessed, totalProcessed)
                .SetProperty(j => j.TotalRows, totalProcessed)
                .SetProperty(j => j.BlobPath, csvBlobPath)
                .SetProperty(j => j.ExcelBlobPath, savedExcelPath)
                .SetProperty(j => j.FileSize, csvStream.Length)
                .SetProperty(j => j.Status, CsvExportStatus.Complete)
                .SetProperty(j => j.CompletedAt, now)
                .SetProperty(j => j.ExpiresAt, expiresAt),
                CancellationToken.None);

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

    private static string SanitizeBlobName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "eksport";

        // Behold kun bokstaver, sifre, bindestrek og understrek
        var sanitized = new string(name
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        return string.IsNullOrEmpty(sanitized) ? "eksport" : sanitized;
    }

    private async Task EnsureBlobStorageConnectionAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(1));
                await _blobStorage.CheckConnectionAsync(timeoutCts.Token);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blob storage tilkoblingsforsøk {Attempt}/{MaxAttempts} feilet", attempt, maxAttempts);

                if (attempt == maxAttempts)
                    throw new InvalidOperationException(
                        $"Kunne ikke koble til blob storage etter {maxAttempts} forsøk. Eksportjobben avbrytes.", ex);

                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}

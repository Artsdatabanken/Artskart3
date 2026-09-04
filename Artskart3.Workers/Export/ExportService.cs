using System.Runtime.CompilerServices;
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
using MiniExcelLibs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Artskart3.Workers.Export;

/// <summary>
/// Kjører CSV- og Excel-eksport: leser observasjoner i batcher og bygger begge formatene samtidig.
///
/// Begge formatene skrives strømmende, slik at minnebruken er flat uansett hvor
/// stor eksporten er: CSV-en går rett til blob storage, mens Excel-filen bygges
/// på lokal disk før den lastes opp. Databasen leses kun én gang — MiniExcel
/// trekker rader fra <c>StreamRowsAsync</c>, som skriver CSV-linjen for hver rad
/// den gir fra seg.
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

        // Institusjons- og samlingskode utledes fra Organization via
        // InstitutionOrgId/DatasetOrgId. Hele tabellen lastes én gang — 25 943
        // rader — i stedet for en join per observasjonsbatch.
        Dictionary<int, string?>? organizationCodes = null;
        if (columns.Contains("InstitutionCode") || columns.Contains("CollectionCode"))
        {
            organizationCodes = await _context.Set<Organization>()
                .AsNoTracking()
                .ToDictionaryAsync(o => o.Id, o => o.Code, cancellationToken);
        }

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

        // Kolonne-visningsnavn (brukes av både CSV og Excel).
        // Excel-arket bruker overskriftene som nøkler, så de må være unike.
        var columnMap = _columnRegistry.GetAllColumns().ToDictionary(c => c.Name, c => c.DisplayName);
        var displayNames = MakeUniqueHeaders(columns.Select(c => columnMap.GetValueOrDefault(c, c)));

        // Excel bygges på lokal disk og lastes opp når den er ferdig. En xlsx er et
        // zip-arkiv, og zip-skriveren trenger en strøm den kan spole i — det har vi
        // ikke mot blob storage. App Service har rikelig med lokal diskplass.
        var tempExcelPath = Path.Combine(
            Path.GetTempPath(), $"artskart-eksport-{job.Id}-{Guid.NewGuid():N}.xlsx");

        var totalProcessed = 0;
        var cancelledByUser = false;
        long csvByteCount;

        try
        {
            await using (var csvBlobStream = await _blobStorage.OpenWriteAsync(csvBlobPath, cancellationToken))
            {
                // CountingStream holder styr på filstørrelsen — vi har ingen
                // MemoryStream å lese Length fra når CSV-en streames.
                var countingStream = new CountingStream(csvBlobStream);
                var writer = new StreamWriter(
                    countingStream, new UTF8Encoding(true), bufferSize: 8192, leaveOpen: true);

                // CSV: header
                await _csvWriter.WriteHeaderAsync(writer, displayNames);

                // Ett gjennomløp av databasen mater begge formatene: MiniExcel trekker
                // rader herfra, og hver rad skriver sin egen CSV-linje på veien ut.
                async IAsyncEnumerable<IDictionary<string, object?>> StreamRowsAsync(
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    var batchSize = _options.Worker.BatchSize;
                    var delayMs = _options.Worker.InterBatchDelayMs;
                    var lastId = 0;

                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        // Sjekk om jobben er kansellert av brukeren
                        var currentStatus = await _context.Set<CsvExportJob>()
                            .Where(j => j.Id == job.Id)
                            .Select(j => j.Status)
                            .FirstAsync(ct);

                        if (currentStatus == CsvExportStatus.Cancelled)
                        {
                            cancelledByUser = true;
                            yield break;
                        }

                        // Hent neste batch med keyset-paginering
                        var batch = await query
                            .Where(o => o.Id > lastId)
                            .OrderBy(o => o.Id)
                            .Take(batchSize)
                            .ToListAsync(ct);

                        if (batch.Count == 0)
                            yield break;

                        foreach (var observation in batch)
                        {
                            var values = columns
                                .Select(c => ExportColumnRegistry.GetValue(observation, c, organizationCodes))
                                .ToList();

                            await _csvWriter.WriteRowAsync(writer, values);

                            var row = new Dictionary<string, object?>(displayNames.Count);
                            for (var col = 0; col < displayNames.Count; col++)
                            {
                                row[displayNames[col]] = FormatExcelValue(values[col]);
                            }

                            yield return row;
                        }

                        totalProcessed += batch.Count;
                        lastId = batch[^1].Id;

                        await _context.Set<CsvExportJob>()
                            .Where(j => j.Id == job.Id)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(j => j.RowsProcessed, totalProcessed),
                                ct);

                        if (delayMs > 0)
                            await Task.Delay(delayMs, ct);
                    }
                }

                await MiniExcel.SaveAsAsync(
                    tempExcelPath,
                    StreamRowsAsync(cancellationToken),
                    printHeader: true,
                    sheetName: "Observasjoner",
                    cancellationToken: cancellationToken);

                // Writeren må tømmes før blob-strømmen committes ved dispose
                await writer.DisposeAsync();
                csvByteCount = countingStream.BytesWritten;
            }

            if (cancelledByUser)
            {
                _logger.LogInformation("Eksportjobb {JobId} ble kansellert", job.Id);
                await DeleteBlobQuietlyAsync(csvBlobPath);
                return;
            }

            // Last opp Excel fra temp-filen
            string? savedExcelPath = null;
            try
            {
                await using var excelFile = File.OpenRead(tempExcelPath);
                await _blobStorage.UploadAsync(excelBlobPath, excelFile, cancellationToken);
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
                    .SetProperty(j => j.FileSize, csvByteCount)
                    .SetProperty(j => j.Status, CsvExportStatus.Complete)
                    .SetProperty(j => j.CompletedAt, now)
                    .SetProperty(j => j.ExpiresAt, expiresAt),
                    CancellationToken.None);

            _logger.LogInformation("Eksportjobb {JobId} fullført. {Rows} rader eksportert", job.Id, totalProcessed);
        }
        catch
        {
            // CSV-en streames, så en avbrutt eksport etterlater en halvskrevet blob.
            // Den må bort, ellers kan brukeren laste ned en ufullstendig fil.
            await DeleteBlobQuietlyAsync(csvBlobPath);
            throw;
        }
        finally
        {
            DeleteTempFileQuietly(tempExcelPath);
        }
    }

    private async Task DeleteBlobQuietlyAsync(string blobPath)
    {
        try
        {
            await _blobStorage.DeleteBlobAsync(blobPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunne ikke slette ufullstendig blob {BlobPath}", blobPath);
        }
    }

    private void DeleteTempFileQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunne ikke slette midlertidig Excel-fil {Path}", path);
        }
    }

    /// <summary>
    /// Radene sendes til Excel som oppslag på kolonneoverskrift, så to kolonner kan
    /// ikke dele visningsnavn. Duplikater nummereres.
    /// </summary>
    private static List<string> MakeUniqueHeaders(IEnumerable<string> names)
    {
        var seen = new Dictionary<string, int>();
        var result = new List<string>();

        foreach (var name in names)
        {
            if (seen.TryGetValue(name, out var count))
            {
                seen[name] = ++count;
                result.Add($"{name} ({count})");
            }
            else
            {
                seen[name] = 1;
                result.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// MiniExcel skriver primitive typer med riktig celletype (tall som tall, datoer
    /// som datoer). Alt annet sendes som tekst.
    /// </summary>
    private static object? FormatExcelValue(object? value)
    {
        return value switch
        {
            null => null,
            string or int or long or double or float or decimal or bool or DateTime => value,
            _ => value.ToString()
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

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous] // TODO: Bytt til [Authorize] når autentisering er satt opp
[Route("api/export/csv")]
public class ExportController : ControllerBase
{
    private readonly ICsvExportService _exportService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(ICsvExportService exportService, IBlobStorageService blobStorageService, ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Returnerer tilgjengelige kolonner for CSV-eksport med visningsnavn og standardvalg.
    /// </summary>
    [HttpGet("columns")]
    public async Task<ActionResult<List<ExportColumnDefinition>>> GetColumns(CancellationToken cancellationToken)
    {
        var columns = await _exportService.GetAvailableColumnsAsync();
        return Ok(columns);
    }

    /// <summary>
    /// Returnerer estimert antall rader og filstørrelse for gitt filter og kolonner.
    /// </summary>
    [HttpPost("summary")]
    public async Task<ActionResult<ExportSummaryDto>> GetSummary([FromBody] StartExportRequestDto request, CancellationToken cancellationToken)
    {
        var summary = await _exportService.GetExportSummaryAsync(request.Filter, request.SelectedColumns);
        return Ok(summary);
    }

    /// <summary>
    /// Starter en ny CSV-eksportjobb. Returnerer jobb-ID for statusforespørsler.
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<object>> StartExport([FromBody] StartExportRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        try
        {
            var jobId = await _exportService.StartExportAsync(userId, request.Filter, request.SelectedColumns);
            return Ok(new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returnerer status og fremdrift for en eksportjobb.
    /// </summary>
    [HttpGet("{jobId:int}/status")]
    public async Task<ActionResult<CsvExportJobDto>> GetStatus(int jobId, CancellationToken cancellationToken)
    {
        var status = await _exportService.GetJobStatusAsync(jobId);
        if (status == null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>
    /// Returnerer nedlastings-URL (SAS) for en ferdig eksportjobb.
    /// </summary>
    [HttpGet("{jobId:int}/download")]
    public async Task<ActionResult<object>> Download(int jobId, CancellationToken cancellationToken)
    {
        var blobPath = await _exportService.GetDownloadUrlAsync(jobId);
        if (blobPath == null)
            return NotFound(new { error = "Filen er ikke klar eller er utløpt." });

        var sasUrl = await _blobStorageService.GenerateSasUrlAsync(blobPath, TimeSpan.FromMinutes(10));
        return Ok(new { url = sasUrl });
    }

    /// <summary>
    /// Returnerer ferdiggenerert Excel-fil fra blob storage.
    /// </summary>
    [HttpGet("{jobId:int}/download/excel")]
    public async Task<ActionResult> DownloadExcel(int jobId, CancellationToken cancellationToken)
    {
        var blobPath = await _exportService.GetDownloadUrlAsync(jobId);
        if (blobPath == null)
            return NotFound(new { error = "Filen er ikke klar eller er utløpt." });

        // Excel-filen ligger ved siden av CSV-filen med .xlsx-endelse
        var excelBlobPath = blobPath.Replace(".csv", ".xlsx");

        await using var excelStream = await _blobStorageService.OpenReadStreamAsync(excelBlobPath, cancellationToken);
        var outputStream = new MemoryStream();
        await excelStream.CopyToAsync(outputStream, cancellationToken);
        outputStream.Position = 0;

        return File(outputStream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"eksport-{jobId}.xlsx");
    }

    /// <summary>
    /// Kansellerer en ventende eller pågående eksportjobb.
    /// </summary>
    [HttpPost("{jobId:int}/cancel")]
    public async Task<ActionResult> Cancel(int jobId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var success = await _exportService.CancelExportAsync(jobId, userId);
        if (!success)
            return Conflict(new { error = "Jobben kan ikke kanselleres." });

        return Ok();
    }

    /// <summary>
    /// Returnerer eksporthistorikk for innlogget bruker.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<CsvExportJobDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var history = await _exportService.GetUserExportHistoryAsync(userId);
        return Ok(history);
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? User.FindFirst("name")?.Value ?? "anonymous";
    }
}

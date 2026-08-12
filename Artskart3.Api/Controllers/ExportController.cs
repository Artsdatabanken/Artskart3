using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/export/csv")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(IExportService exportService, IBlobStorageService blobStorageService, ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("columns")]
    public async Task<ActionResult<List<ExportColumnDefinition>>> GetColumns(CancellationToken cancellationToken)
    {
        var columns = await _exportService.GetAvailableColumnsAsync();
        return Ok(columns);
    }

    [HttpPost("summary")]
    public async Task<ActionResult<ExportSummaryDto>> GetSummary([FromBody] StartExportRequestDto? request, CancellationToken cancellationToken)
    {
        if (request?.Filter == null)
            return BadRequest(new { error = "Filter er påkrevd." });

        var summary = await _exportService.GetExportSummaryAsync(request.Filter, request.SelectedColumns ?? [], request.Name, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("start")]
    public async Task<ActionResult<object>> StartExport([FromBody] StartExportRequestDto? request, CancellationToken cancellationToken)
    {
        if (request?.Filter == null)
            return BadRequest(new { error = "Filter er påkrevd." });

        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        try
        {
            var jobId = await _exportService.StartExportAsync(userId, request.Filter, request.SelectedColumns ?? [], request.Name, cancellationToken);
            return Ok(new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{jobId:int}/status")]
    public async Task<ActionResult<CsvExportJobDto>> GetStatus(int jobId, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var status = await _exportService.GetJobStatusAsync(jobId, userId, cancellationToken);
        if (status == null)
            return NotFound();

        return Ok(status);
    }

    [HttpGet("{jobId:int}/download")]
    public async Task<ActionResult> Download(int jobId, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var blobPath = await _exportService.GetCsvBlobPathAsync(jobId, userId, cancellationToken);
        if (blobPath == null)
            return NotFound(new { error = "Filen er ikke klar eller er utløpt." });

        var stream = await _blobStorageService.OpenReadStreamAsync(blobPath, cancellationToken);
        var fileName = Path.GetFileName(blobPath);
        return File(stream, "text/csv", fileName);
    }

    [HttpGet("{jobId:int}/download/excel")]
    public async Task<ActionResult> DownloadExcel(int jobId, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var excelBlobPath = await _exportService.GetExcelBlobPathAsync(jobId, userId, cancellationToken);
        if (excelBlobPath == null)
            return NotFound(new { error = "Excel-filen er ikke tilgjengelig." });

        var stream = await _blobStorageService.OpenReadStreamAsync(excelBlobPath, cancellationToken);
        var fileName = Path.GetFileName(excelBlobPath);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("{jobId:int}/cancel")]
    public async Task<ActionResult> Cancel(int jobId, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var success = await _exportService.CancelExportAsync(jobId, userId, cancellationToken);
        if (!success)
            return Conflict(new { error = "Jobben kan ikke kanselleres." });

        return Ok();
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<CsvExportJobDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var history = await _exportService.GetUserExportHistoryAsync(userId, cancellationToken);
        return Ok(history);
    }

    private ActionResult? TryGetUserId(out string userId)
    {
        userId = User.FindFirst("sub")?.Value ?? User.FindFirst("name")?.Value ?? "";
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Bruker mangler 'sub'- eller 'name'-claim." });
        return null;
    }
}

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/export/csv")]
public class ExportController : ControllerBase
{
    private const string AnonymousUserHeader = "X-Anonymous-User-Id";

    private readonly IExportService _exportService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAnonymousUserTokenService _anonymousUserTokens;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IExportService exportService,
        IBlobStorageService blobStorageService,
        IAnonymousUserTokenService anonymousUserTokens,
        ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _blobStorageService = blobStorageService;
        _anonymousUserTokens = anonymousUserTokens;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("columns")]
    public async Task<ActionResult<List<ExportColumnDefinition>>> GetColumns(CancellationToken cancellationToken)
    {
        var columns = await _exportService.GetAvailableColumnsAsync();
        return Ok(columns);
    }

    [AllowAnonymous]
    [HttpPost("summary")]
    public async Task<ActionResult<ExportSummaryDto>> GetSummary([FromBody] StartExportRequestDto? request, CancellationToken cancellationToken)
    {
        if (request?.Filter == null)
            return BadRequest(new { error = "Filter er påkrevd." });

        if (ValidateFilter(request.Filter) is { } filterError)
            return filterError;

        var summary = await _exportService.GetExportSummaryAsync(request.Filter, request.SelectedColumns ?? [], request.Name, cancellationToken);
        return Ok(summary);
    }

    [AllowAnonymous]
    [HttpPost("start")]
    public async Task<ActionResult<object>> StartExport([FromBody] StartExportRequestDto? request, CancellationToken cancellationToken)
    {
        if (request?.Filter == null)
            return BadRequest(new { error = "Filter er påkrevd." });

        if (ValidateFilter(request.Filter) is { } filterError)
            return filterError;

        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var isAnonymous = User.Identity?.IsAuthenticated != true;

        try
        {
            var jobId = await _exportService.StartExportAsync(userId, isAnonymous, request.Filter, request.SelectedColumns ?? [], request.Name, cancellationToken);
            return Ok(new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
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

    [AllowAnonymous]
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

    [AllowAnonymous]
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

    [AllowAnonymous]
    [HttpGet("history")]
    public async Task<ActionResult<List<CsvExportJobDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var history = await _exportService.GetUserExportHistoryAsync(userId, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Utsteder en signert anonym bruker-ID. Klienten lagrer tokenet i
    /// localStorage og sender det som X-Anonymous-User-Id-header.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("anonymous-session")]
    public ActionResult<object> CreateAnonymousSession(CancellationToken cancellationToken)
    {
        return Ok(new { token = _anonymousUserTokens.CreateToken() });
    }

    [HttpPost("claim-anonymous")]
    public async Task<ActionResult<object>> ClaimAnonymousExports(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
            return Unauthorized(new { error = "Bruker mangler gyldig 'sub'-claim." });

        // Bare token serveren selv har signert godtas — ellers kunne en innlogget
        // bruker sendt en annen brukers sub her og flyttet hele eksporthistorikken
        // deres over til sin egen konto.
        var token = Request.Headers[AnonymousUserHeader].FirstOrDefault();
        if (!_anonymousUserTokens.TryGetUserId(token, out var anonymousUserId))
            return BadRequest(new { error = $"Mangler gyldig {AnonymousUserHeader}-header." });

        var claimedCount = await _exportService.ClaimAnonymousExportsAsync(userId, anonymousUserId, cancellationToken);
        return Ok(new { claimedCount });
    }

    /// <summary>
    /// Samme filterregler som søket. Eksporten gikk tidligere rett i
    /// ObservationQueryBuilder uten validering, og der smalnes RegistrationStatusId
    /// til byte med en unchecked cast — 258 ble 2, og CSV-en inneholdt «ikke påvist»
    /// uten at noen hadde bedt om det. Et lagret filter kan dessuten være laget før
    /// reglene ble strengere, så det må valideres når det brukes, ikke bare når det
    /// lages.
    /// </summary>
    private BadRequestObjectResult? ValidateFilter(ObservationSearchFilterDto filter)
    {
        var error = ObservationFilterValidator.GetError(filter);
        return error == null ? null : BadRequest(new { error });
    }

    /// <summary>
    /// Innloggede brukere identifiseres med «sub»-claimet. Anonyme brukere må ha et
    /// token serveren selv har signert (se /anonymous-session), sendt som
    /// X-Anonymous-User-Id-header. Signaturen beviser at ID-en er utstedt som anonym,
    /// så en sub kan ikke spoofes gjennom headeren.
    /// </summary>
    private ActionResult? TryGetUserId(out Guid userId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // En autentisert bruker med ugyldig sub er en feiltilstand, ikke en
            // anonym bruker — ikke fall tilbake på headeren og lagre jobben på en
            // ID som ikke har noe med kontoen å gjøre.
            if (Guid.TryParse(User.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty)
                return null;

            return Unauthorized(new { error = "Bruker mangler gyldig 'sub'-claim." });
        }

        if (_anonymousUserTokens.TryGetUserId(Request.Headers[AnonymousUserHeader].FirstOrDefault(), out userId))
            return null;

        return Unauthorized(new { error = $"Mangler brukeridentitet ('sub'-claim eller {AnonymousUserHeader}-header)." });
    }
}

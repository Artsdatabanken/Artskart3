using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class WarningsController : ControllerBase
{
    private readonly IWarningsService _warningsService;
    private readonly ILogger<WarningsController> _logger;

    public WarningsController(IWarningsService warningsService, ILogger<WarningsController> logger)
    {
        _warningsService = warningsService ?? throw new ArgumentNullException(nameof(warningsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns all configured warnings.
    /// The client filters by startDisplayDate / endDisplayDate to determine which warnings to show.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<WarningModel>>> GetWarnings()
    {
        try
        {
            var warnings = await _warningsService.GetAllWarningsAsync();
            return Ok(warnings);
        }
        catch (ApplicationException ex)
        {
            _logger.LogWarning(ex, "Application error retrieving warnings");
            return StatusCode(503, new { error = "An error occurred while retrieving warnings. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving warnings");
            return StatusCode(500, new { error = "An unexpected error occurred. Please try again later." });
        }
    }
}

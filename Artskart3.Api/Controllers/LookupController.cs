using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class LookupController : ControllerBase
    {
        private readonly ILookupService _lookupService;
        private readonly ILogger<LookupController> _logger;

        public LookupController(ILookupService lookupService, ILogger<LookupController> logger)
        {
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns all categories with their category type, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("Categories")]
        [Produces("application/json")]
        public async Task<ActionResult<CategoryListDto>> GetCategories()
        {
            try
            {
                var categorylist = await _lookupService.GetCategoriesAsync();
                return Ok(categorylist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving categories");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }
    }
}

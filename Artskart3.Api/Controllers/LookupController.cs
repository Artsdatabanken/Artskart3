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
        public async Task<ActionResult<IEnumerable<CategoryTypeDto>>> GetCategories()
        {
            try
            {
                var categories = await _lookupService.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving categories");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        /// <summary>
        /// Returns all area types with their areas, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("Areas")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<AreaTypeDto>>> GetAreas()
        {
            try
            {
                var areas = await _lookupService.GetAreasAsync();
                return Ok(areas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving areas");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }
    }
}

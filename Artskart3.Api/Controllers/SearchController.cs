using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artskart3.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        }

        [HttpGet("SearchTaxons")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<Taxon>>> SearchTaxons([FromQuery] string name, [FromQuery] int maxCount = 20)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Name parameter is required.");

            if (maxCount < 1 || maxCount > 1000)
                return BadRequest("Max count must be between 1 and 1000.");

            var taxons = await _searchService.GetTaxonsAsync(name, maxCount);
            return Ok(taxons);
        }

        [HttpGet("Locations")]
        [Produces("application/json")]
        public async Task<ActionResult<string>> GetLocations([FromQuery] LocationSearchFilterDto? filter = null)
        {
            try
            {
                filter = filter ?? new LocationSearchFilterDto();

                if (filter.MaxResults < 1 || filter.MaxResults > 10000)
                {
                    return BadRequest(new { error = "MaxResults must be between 1 and 10000." });
                }
                
                if (filter.CoordinatePrecisionFrom > 0 && filter.CoordinatePrecisionTo > 0 && filter.CoordinatePrecisionFrom > filter.CoordinatePrecisionTo)
                {
                    return BadRequest(new { error = "CoordinatePrecisionFrom must be less than or equal to CoordinatePrecisionTo." });
                }               

                var result = await _searchService.GetLocationsAsync(filter);
                return Ok(result);
            }
            catch (ApplicationException)
            {
                return StatusCode(503, new { error = "An error occurred while retrieving locations. Please try again later." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }
    }
}


using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
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
            _searchService = searchService;
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
    }
}

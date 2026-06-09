using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Api.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Artskart3.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ISearchService searchService, ILogger<SearchController> logger)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Searches for taxa by scientific or common name with fuzzy matching.
        /// Returns up to maxCount results matching exact, starts-with, or contains patterns.
        /// </summary>
        [HttpGet("SearchTaxons")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<Taxon>>> SearchTaxons(
            [FromQuery] string name,
            [FromQuery] int maxCount = SearchConstants.DefaultMaxTaxonCount)
        {
            try
            {
                if (!ValidateTaxonSearchInput(name, maxCount, out var validationError))
                    return validationError;

                var taxons = await _searchService.GetTaxonsAsync(name, maxCount);
                _logger.LogInformation("Retrieved {Count} taxons for search term: {Name}", taxons.Count(), name);
                return Ok(taxons);
            }
            catch (ApplicationException ex)
            {
                _logger.LogError(ex, "Application error during taxon search");
                return StatusCode(503, new { error = SearchConstants.ServiceUnavailableMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during taxon search");
                return StatusCode(500, new { error = SearchConstants.UnexpectedErrorMessage });
            }
        }

        /// <summary>
        /// Searches for observation locations filtered by taxon group, collection, category, basis of record, and coordinate precision.
        /// Returns aggregated observation counts grouped by location with UTM Zone 33N coordinates.
        /// Defaults to MaxResults = 1000.
        /// </summary>
        [HttpGet("Locations")]
        [Produces("application/json")]
        public async Task<ActionResult<string>> GetObservationLocations([FromQuery] LocationSearchFilterDto? filter = null)
        {
            try
            {
                filter ??= new LocationSearchFilterDto();

                if (!ValidateLocationSearchFilter(filter, out var validationError))
                    return validationError;

                var result = await _searchService.GetLocationsAsync(filter);
                _logger.LogInformation("Retrieved observation location data for maxResults: {MaxResults}", filter.MaxResults);
                return Content(result, "application/json");
            }
            catch (ApplicationException ex)
            {
                _logger.LogError(ex, "Application error during location search");
                return StatusCode(503, new { error = SearchConstants.ServiceUnavailableMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during location search");
                return StatusCode(500, new { error = SearchConstants.UnexpectedErrorMessage });
            }
        }

        /// <summary>
        /// Retrieves all area markers (counties and municipalities) with aggregated observation counts and WKT polygons.
        /// </summary>
        [HttpGet("AreasObservations")]
        [Produces("application/json")]
        public async Task<ActionResult<AreaMarkerDto[]>> GetAreasObservations([FromQuery] int zoomLevel = 1)
        {
            try
            {
                var areas = await _searchService.GetObservationsByZoomLevelAsync(zoomLevel);
                _logger.LogInformation("Retrieved {Count} area markers for zoom level {ZoomLevel}", areas.Count(), zoomLevel);
                return Ok(areas.ToArray());
            }
            catch (ApplicationException ex)
            {
                _logger.LogError(ex, "Application error during area observations retrieval");
                return StatusCode(503, new { error = SearchConstants.ServiceUnavailableMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during area observations retrieval");
                return StatusCode(500, new { error = SearchConstants.UnexpectedErrorMessage });
            }
        }

        /// <summary>
        /// Validates taxon search input parameters.
        /// </summary>
        private bool ValidateTaxonSearchInput(string name, int maxCount, out BadRequestObjectResult? validationError)
        {
            validationError = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                validationError = BadRequest(new { error = "Name parameter is required." });
                return false;
            }

            if (!IsValidMaxResultCount(maxCount, SearchConstants.MinTaxonResults, SearchConstants.MaxTaxonCount))
            {
                validationError = BadRequest(CreateRangeErrorMessage(SearchConstants.MinTaxonResults, SearchConstants.MaxTaxonCount));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates location search filter parameters.
        /// </summary>
        private bool ValidateLocationSearchFilter(LocationSearchFilterDto filter, out BadRequestObjectResult? validationError)
        {
            validationError = null;

            if (!IsValidMaxResultCount(filter.MaxResults, SearchConstants.MinLocationResults, SearchConstants.MaxLocationResults))
            {
                validationError = BadRequest(CreateRangeErrorMessage(SearchConstants.MinLocationResults, SearchConstants.MaxLocationResults));
                return false;
            }

            if (!IsValidCoordinatePrecisionRange(filter.CoordinatePrecisionFrom, filter.CoordinatePrecisionTo))
            {
                validationError = BadRequest(new { error = SearchConstants.CoordinatePrecisionInvalidMessage });
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a result count falls within acceptable range.
        /// </summary>
        private bool IsValidMaxResultCount(int maxCount, int minValue, int maxValue)
            => maxCount >= minValue && maxCount <= maxValue;

        /// <summary>
        /// Checks if coordinate precision range is valid (From ≤ To).
        /// </summary>
        private bool IsValidCoordinatePrecisionRange(int from, int to)
            => !(from > 0 && to > 0 && from > to);

        /// <summary>
        /// Creates a standardized range validation error message.
        /// </summary>
        private object CreateRangeErrorMessage(int min, int max)
            => new { error = $"Value must be between {min} and {max}." };
    }
}

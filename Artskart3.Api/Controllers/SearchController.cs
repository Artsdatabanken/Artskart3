using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using AreaType = Artskart3.Core.Domain.Enums.AreaType;
using Artskart3.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Artskart3.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private const int DefaultMaxTaxonCount = 20;
        private const int MaxTaxonCount = 1000;
        private const int MinResults = 1;
        private const int MaxResults = 10000;
        private const int DefaultMaxObservationCount = 100;

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
        public async Task<ActionResult<IEnumerable<Taxon>>> SearchTaxons([FromQuery] string name, [FromQuery] int maxCount = DefaultMaxTaxonCount, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Name parameter is required.");

            if (maxCount < MinResults || maxCount > MaxTaxonCount)
                return BadRequest($"Max count must be between {MinResults} and {MaxTaxonCount}.");

            try
            {
                var taxons = await _searchService.GetTaxonsAsync(name, maxCount, cancellationToken);
                return Ok(taxons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved søk etter takson med navn: {TaxonName}", name);
                throw; // håndteres av global filter
            }
        }

        /// <summary>
        /// Searches for observation locations filtered by taxon group, collection, category, basis of record, and coordinate precision.
        /// Returns aggregated observation counts grouped by location with UTM Zone 33N coordinates.
        /// Defaults to MaxResults = 1000
        /// </summary>
        [HttpGet("Locations")]
        [Produces("application/json")]
        public async Task<ActionResult<string>> GetLocations([FromQuery] LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            filter = filter ?? new LocationSearchFilterDto();

            if (filter.MaxResults < MinResults || filter.MaxResults > MaxResults)
            {
                return BadRequest(new { error = $"MaxResults must be between {MinResults} and {MaxResults}." });
            }

            if (filter.CoordinatePrecisionFrom > 0 && filter.CoordinatePrecisionTo > 0 && filter.CoordinatePrecisionFrom > filter.CoordinatePrecisionTo)
            {
                return BadRequest(new { error = "CoordinatePrecisionFrom must be less than or equal to CoordinatePrecisionTo." });
            }

            _logger.LogInformation("Retrieving locations with filter. MaxResults: {MaxResults}", filter.MaxResults);

            try
            {
                var result = await _searchService.GetLocationsAsync(filter, cancellationToken);
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved henting av lokasjoner med MaxResults: {MaxResults}", filter.MaxResults);
                throw; // håndteres av global filter
            }
        }

        /// <summary>
        /// Searches for observations using optional filters.
        /// When PageNumber and ResultsPerPage are provided, returns a paginated response with metadata.
        /// When pagination parameters are omitted, returns a flat list capped at DefaultMaxObservationCount.
        /// </summary>
        [HttpPost("Observation")]
        [Produces("application/json")]
        [ServiceFilter(typeof(SlowQueryLoggingFilter))]
        public async Task<ActionResult<PagedObservationResponseDto>> GetObservations([FromBody] ObservationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            filter ??= new ObservationSearchFilterDto();

            if (filter.IsPaginated)
            {
                if (filter.PageNumber < 1)
                    return BadRequest(new { error = "PageNumber must be 1 or greater." });

                if (filter.ResultsPerPage < MinResults || filter.ResultsPerPage > MaxResults)
                    return BadRequest(new { error = $"ResultsPerPage must be between {MinResults} and {MaxResults}." });
            }

            try
            {
                if (filter.IsPaginated)
                {
                    var allItems = await _searchService.GetObservationsAsync(filter, cancellationToken);
                    var resultsPerPage = filter.ResultsPerPage!.Value;
                    var pagedResult = new PagedObservationResponseDto
                    {
                        Items = allItems.Take(resultsPerPage),
                        PageNumber = filter.PageNumber!.Value,
                        ResultsPerPage = resultsPerPage,
                        LookaheadCount = (allItems.Count / resultsPerPage) - 1
                    };

                    return Ok(pagedResult);
                }

                var results = await _searchService.GetObservationsAsync(filter, cancellationToken);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved henting av observasjoner med filter: {@Filter}", filter);
                throw; // håndteres av global filter
            }
        }

        /// <summary>
        /// Retrieves all area markers (counties and municipalities) with aggregated observation counts and centroids.
        /// </summary>
        [HttpGet("Areas")]
        [Produces("application/json")]
        public async Task<ActionResult<AreaMarkerDto[]>> GetAreas(CancellationToken cancellationToken = default)
        {
            try
            {
                var areas = await _searchService.GetAreasByTypeIdsAsync([(int)AreaType.Municipality, (int)AreaType.County], cancellationToken);
                return Ok(areas.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved henting av områder");
                throw; // håndteres av global filter
            }
        }
    }
}

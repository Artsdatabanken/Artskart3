using Artskart3.Api.Filters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ISpeciesService _speciesService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchService searchService, ISpeciesService speciesService, ILogger<SearchController> logger)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _speciesService = speciesService ?? throw new ArgumentNullException(nameof(speciesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Searches for taxa by scientific or common name with fuzzy matching.
    /// Returns up to maxCount results matching exact, starts-with, or contains patterns.
    /// </summary>
    [HttpGet("SearchTaxons")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<TaxonDto>>> SearchTaxons(
        [FromQuery] string name,
        [FromQuery] int maxCount = SearchConstants.DefaultMaxTaxonCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ValidateTaxonSearchInput(name, maxCount, out var validationError))
            {
                // validationError skal aldri være null når en validering feiler
                return validationError!;
            }

            var taxons = await _searchService.GetTaxonsAsync(name, maxCount, cancellationToken);
            _logger.LogInformation("Retrieved {Count} taxons for search term: {Name}", taxons.Count(), name);
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
    /// Defaults to MaxResults = 1000.
    /// </summary>
    [HttpPost("Locations")]
    [Produces("application/json")]
    public async Task<ActionResult<string>> GetObservationLocations([FromBody] LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filter ??= new LocationSearchFilterDto();

            if (!ValidateLocationSearchFilter(filter, out var validationError))
            {
                // validationError skal aldri være null når en validering feiler
                return validationError!;
            }
            var result = await _searchService.GetLocationsAsync(filter, cancellationToken);
            _logger.LogInformation("Retrieved observation location data for maxResults: {MaxResults}", filter.MaxResults);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feil ved henting av lokasjoner med MaxResults: {MaxResults}", filter?.MaxResults);
            throw; // håndteres av global filter
        }
    }

    /// <summary>
    /// Searches for observations using optional filters.
    /// When PageNumber and ResultsPerPage are provided, returns a paginated response with metadata.
    /// When pagination parameters are omitted, returns a flat list capped at <see cref="SearchConstants.DefaultMaxObservations"/> (20) results.
    /// PageNumber and ResultsPerPage must both be provided, or both be omitted.
    /// </summary>
    [HttpPost("Observation")]
    [Produces("application/json")]
    [ServiceFilter(typeof(SlowQueryLoggingFilter))]
    public async Task<ActionResult<PagedObservationResponseDto>> GetObservations([FromBody] ObservationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new ObservationSearchFilterDto();

        if (!ValidateObservationSearchFilter(filter, out var validationError))
        {
            // validationError skal aldri være null når en validering feiler
            return validationError!;
        }

        try
        {
            if (filter.IsPaginated)
            {
                // Repositoriet har allerede gjort Skip i SQL — her henter vi bare første side fra lookahead-vinduet
                var allItems = await _searchService.GetObservationsAsync(filter, cancellationToken);
                var resultsPerPage = filter.ResultsPerPage!.Value;
                var pageNumber = filter.PageNumber!.Value;
                var pagedResult = new PagedObservationResponseDto
                {
                    Items = allItems.Take(resultsPerPage),
                    PageNumber = pageNumber,
                    ResultsPerPage = resultsPerPage,
                    LookaheadCount = Math.Max(0, (allItems.Count + resultsPerPage - 1) / resultsPerPage - 1)
                };

                return Ok(pagedResult);
            }

            var results = await _searchService.GetObservationsAsync(filter, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feil ved henting av observasjoner med filter: {Filter}", filter);
            throw; // håndteres av global filter
        }
    }


    /// <summary>
    /// Retrieves all area markers (counties and municipalities) with aggregated observation counts and WKT polygons.
    /// When filters are provided, observation counts are computed dynamically.
    /// </summary>
    [HttpPost("AreaMarkers")]
    [Produces("application/json")]
    public async Task<ActionResult<AreaMarkerDto[]>> GetAreaMarkers([FromQuery] int zoomLevel = 1, [FromBody] LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (zoomLevel < 1 || zoomLevel > 2)
            {
                return BadRequest(new { error = "zoomLevel must be 1 (counties) or 2 (municipalities)." });
            }

            if (filter != null && !ValidateLocationSearchFilter(filter, out var validationError))
            {
                return validationError!;
            }

            var areas = await _searchService.GetAreaMarkersAsync(zoomLevel, filter, cancellationToken);
            _logger.LogInformation("Retrieved {Count} area markers for zoom level {ZoomLevel}", areas.Count(), zoomLevel);
            return Ok(areas.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feil ved henting av områder");
            throw; // håndteres av global filter
        }
    }

    /// <summary>
    /// Søker etter arter via NorTaxa-API.
    /// Hvis input er et heltall, slås det opp direkte på taxonId.
    /// Ellers utføres et navnesøk med maks 20 resultater.
    /// </summary>
    [HttpGet("Species")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<SpeciesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<List<SpeciesDto>>> SearchSpecies(
        [FromQuery] string search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest(new { error = "Search parameter is required." });
        }

        try
        {
            var results = await _speciesService.SearchSpeciesAsync(search, cancellationToken);
            _logger.LogInformation("Species-søk for '{Search}' returnerte {Count} resultater", search, results.Count);
            return Ok(results);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("NorTaxa API timeout ved søk: {Search}", search);
            return StatusCode(504, new { error = "NorTaxa API did not respond within the timeout period." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "NorTaxa API utilgjengelig ved søk: {Search}", search);
            return StatusCode(502, new { error = "Unable to contact NorTaxa API." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feil ved artssøk med søkestreng: {Search}", search);
            throw; // håndteres av global filter
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
    /// Validates observation search filter parameters.
    /// </summary>
    private bool ValidateObservationSearchFilter(ObservationSearchFilterDto filter, out BadRequestObjectResult? validationError)
    {
        validationError = null;

        if (filter.PageNumber.HasValue != filter.ResultsPerPage.HasValue)
        {
            validationError = BadRequest(new { error = "PageNumber and ResultsPerPage must both be provided, or both be omitted." });
            return false;
        }

        if (filter.PageNumber != null && filter.PageNumber.Value < 1)
        {
            validationError = BadRequest(new { error = "PageNumber must be greater than or equal to 1." });
            return false;
        }

        if (filter.ResultsPerPage != null && !IsValidMaxResultCount(filter.ResultsPerPage.Value, SearchConstants.MinObservationResults, SearchConstants.MaxObservationResults))
        {
            validationError = BadRequest(CreateRangeErrorMessage(SearchConstants.MinObservationResults, SearchConstants.MaxObservationResults));
            return false;
        }

        if (filter.CoordinatePrecision?.From != null && filter.CoordinatePrecision?.To != null && !IsValidCoordinatePrecisionRange(filter.CoordinatePrecision.From.Value, filter.CoordinatePrecision.To.Value))
        {
            validationError = BadRequest(new { error = SearchConstants.CoordinatePrecisionInvalidMessage });
            return false;
        }

        if (!ValidateFilterArraySizes(filter, out validationError))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validerer at ingen filter-arrayer overskrider maksimal størrelse.
    /// </summary>
    private bool ValidateFilterArraySizes(ObservationSearchFilterDto filter, out BadRequestObjectResult? validationError)
    {
        validationError = null;
        var max = SearchConstants.MaxFilterArraySize;

        ReadOnlySpan<(string name, int? length)> arrays =
        [
            (nameof(filter.TaxonGroupIds), filter.TaxonGroupIds?.Length),
            (nameof(filter.CategoryIds), filter.CategoryIds?.Length),
            (nameof(filter.OrganizationIds), filter.OrganizationIds?.Length),
            (nameof(filter.MunicipalityIds), filter.MunicipalityIds?.Length),
            (nameof(filter.CountyIds), filter.CountyIds?.Length),
            (nameof(filter.RestrictedAreaIds), filter.RestrictedAreaIds?.Length),
            (nameof(filter.OceanAreaIds), filter.OceanAreaIds?.Length),
            (nameof(filter.BehaviorIds), filter.BehaviorIds?.Length),
            (nameof(filter.BasisOfRecordIds), filter.BasisOfRecordIds?.Length),
        ];

        foreach (var (name, length) in arrays)
        {
            if (length > max)
            {
                validationError = BadRequest(new { error = $"{name} can contain at most {max} items." });
                return false;
            }
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

        if (filter.CoordinatePrecision?.From != null && filter.CoordinatePrecision?.To != null && !IsValidCoordinatePrecisionRange(filter.CoordinatePrecision.From.Value, filter.CoordinatePrecision.To.Value))
        {
            validationError = BadRequest(new { error = SearchConstants.CoordinatePrecisionInvalidMessage });
            return false;
        }

        if (filter.Envelope != null && !filter.Envelope.IsValid)
        {
            validationError = BadRequest(new { error = "Envelope bounds are invalid: MinX must be less than MaxX and MinY must be less than MaxY." });
            return false;
        }

        if (!ValidateLocationFilterArraySizes(filter, out validationError))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validerer at ingen filter-arrayer i lokasjonsfilter overskrider maksimal størrelse.
    /// </summary>
    private bool ValidateLocationFilterArraySizes(LocationSearchFilterDto filter, out BadRequestObjectResult? validationError)
    {
        validationError = null;
        var max = SearchConstants.MaxFilterArraySize;

        ReadOnlySpan<(string name, int? length)> arrays =
        [
            (nameof(filter.TaxonGroupIds), filter.TaxonGroupIds?.Length),
            (nameof(filter.CategoryIds), filter.CategoryIds?.Length),
            (nameof(filter.OrganizationIds), filter.OrganizationIds?.Length),
            (nameof(filter.MunicipalityIds), filter.MunicipalityIds?.Length),
            (nameof(filter.CountyIds), filter.CountyIds?.Length),
            (nameof(filter.RestrictedAreaIds), filter.RestrictedAreaIds?.Length),
            (nameof(filter.OceanAreaIds), filter.OceanAreaIds?.Length),
            (nameof(filter.BehaviorIds), filter.BehaviorIds?.Length),
            (nameof(filter.BasisOfRecordIds), filter.BasisOfRecordIds?.Length),
        ];

        foreach (var (name, length) in arrays)
        {
            if (length > max)
            {
                validationError = BadRequest(new { error = $"{name} can contain at most {max} items." });
                return false;
            }
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

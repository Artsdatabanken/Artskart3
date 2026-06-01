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
        public async Task<ActionResult<IEnumerable<CategoryTypeDto>>> GetCategories(CancellationToken cancellationToken = default)
        {
            try
            {
                var categories = await _lookupService.GetCategoriesAsync(cancellationToken);
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
        public async Task<ActionResult<IEnumerable<AreaTypeDto>>> GetAreas(CancellationToken cancellationToken = default)
        {
            try
            {
                var areas = await _lookupService.GetAreasAsync(cancellationToken);
                return Ok(areas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving areas");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        /// <summary>
        /// Returns all institutions, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("Institutions")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<InstitutionDto>>> GetInstitutions(CancellationToken cancellationToken = default)
        {
            try
            {
                var institutions = await _lookupService.GetInstitutionsAsync(cancellationToken);
                return Ok(institutions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving institutions");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        /// <summary>
        /// Returns all taxon groups, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("TaxonGroups")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<TaxonGroupDto>>> GetTaxonGroups(CancellationToken cancellationToken = default)
        {
            try
            {
                var taxonGroups = await _lookupService.GetTaxonGroupsAsync(cancellationToken);
                return Ok(taxonGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving taxon groups");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        /// <summary>
        /// Returns all behaviors, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("Behaviors")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<BehaviorDto>>> GetBehaviors(CancellationToken cancellationToken = default)
        {
            try
            {
                var behaviors = await _lookupService.GetBehaviorsAsync(cancellationToken);
                return Ok(behaviors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving behaviors");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        /// <summary>
        /// Returns all basis of record types, intended for populating filter dropdowns.
        /// </summary>
        [HttpGet("BasisOfRecords")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<BasisOfRecordDto>>> GetBasisOfRecords(CancellationToken cancellationToken = default)
        {
            try
            {
                var basisOfRecords = await _lookupService.GetBasisOfRecordsAsync(cancellationToken);
                return Ok(basisOfRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving basis of records");
                return StatusCode(500, new { error = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }
    }
}

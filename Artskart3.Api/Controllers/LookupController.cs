using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class LookupController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly ITaxonHierarchyService _taxonHierarchy;

    public LookupController(ILookupService lookupService, ITaxonHierarchyService taxonHierarchy)
    {
        _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
        _taxonHierarchy = taxonHierarchy ?? throw new ArgumentNullException(nameof(taxonHierarchy));
    }

    /// <summary>
    /// Returns all categories with their category type, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("Categories")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<CategoryTypeDto>>> GetCategories(CancellationToken cancellationToken = default)
    {
        var categories = await _lookupService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Returns all area types with their areas, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("Areas")]
    [Produces("application/json")]
    public async Task<ActionResult<AreaResponseDto>> GetAreas(CancellationToken cancellationToken = default)
    {
        var areas = await _lookupService.GetAreasAsync(cancellationToken);
        return Ok(areas);
    }

    /// <summary>
    /// Returns all institutions, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("Institutions")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<InstitutionDto>>> GetInstitutions(CancellationToken cancellationToken = default)
    {
        var institutions = await _lookupService.GetInstitutionsAsync(cancellationToken);
        return Ok(institutions);
    }

    /// <summary>
    /// Returns organizations by name up to maxCount results matching the search term.
    /// </summary>
    [HttpGet("Organizations")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetSearchOrganizations(
        [FromQuery] string search,
        [FromQuery] int maxCount = SearchConstants.DefaultMaxOrganizationCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Ok(Enumerable.Empty<OrganizationDto>());
        }

        if (maxCount < 1 || maxCount > SearchConstants.MaxOrganizationCount)
        {
            return BadRequest(new { error = $"maxCount must be between 1 and {SearchConstants.MaxOrganizationCount}." });
        }

        var organizations = await _lookupService.SearchOrganizationsAsync(search, maxCount, cancellationToken);
        return Ok(organizations);
    }

    // OrganizationType: 1 = Publisher, 2 = Dataset, 3 = Project (GBIF-navn).
    //
    // Navnene i koden fulgte etter 1. september 2026. FØR den datoen het type 2
    // «Collection» og type 3 «Dataset» — så et gammelt «DatasetOrgId» betyr type 3,
    // mens dagens betyr type 2. De byttet plass, de flyttet seg ikke ett hakk.
    // Det gjelder alt fra før den datoen: spørringer, planer, eksportfiler og
    // API-kall mot /api/Lookup/Datasets.
    private const int DatasetOrganizationTypeId = 2;
    private const int ProjectOrganizationTypeId = 3;

    /// <summary>
    /// Typeahead for datasett — organisasjonstype 2. Returnerer OrganizationId som
    /// sendes som datasetOrgId i søkefilteret.
    /// </summary>
    [HttpGet("Datasets")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetSearchDatasets(
        [FromQuery] string search,
        [FromQuery] int maxCount = SearchConstants.DefaultMaxOrganizationCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Ok(Enumerable.Empty<OrganizationDto>());
        }

        if (maxCount < 1 || maxCount > SearchConstants.MaxOrganizationCount)
        {
            return BadRequest(new { error = $"maxCount must be between 1 and {SearchConstants.MaxOrganizationCount}." });
        }

        var result = await _lookupService.SearchOrganizationsByTypeAsync(
            search, DatasetOrganizationTypeId, maxCount, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Typeahead for prosjekt — organisasjonstype 3. Returnerer OrganizationId som
    /// sendes som projectOrgId i søkefilteret.
    /// </summary>
    [HttpGet("Projects")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetSearchProjects(
        [FromQuery] string search,
        [FromQuery] int maxCount = SearchConstants.DefaultMaxOrganizationCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Ok(Enumerable.Empty<OrganizationDto>());
        }

        if (maxCount < 1 || maxCount > SearchConstants.MaxOrganizationCount)
        {
            return BadRequest(new { error = $"maxCount must be between 1 and {SearchConstants.MaxOrganizationCount}." });
        }

        var result = await _lookupService.SearchOrganizationsByTypeAsync(
            search, ProjectOrganizationTypeId, maxCount, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Typeahead for katalognummer. PREFIKSSØK — hvert treff kommer med
    /// ObservationId-ene det peker på, som sendes som observationIds i filteret.
    ///
    /// Grunnen til at treffet bærer IDer og ikke bare strengen: katalognummer er
    /// tilnærmet unikt, og et filter på ID blir et seek på klyngeindeksen i stedet
    /// for et delstrengsøk mot 61M rader.
    /// </summary>
    [HttpGet("CatalogNumbers")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<CatalogNumberMatchDto>>> GetSearchCatalogNumbers(
        [FromQuery] string search,
        [FromQuery] int maxCount = SearchConstants.DefaultMaxOrganizationCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return Ok(Enumerable.Empty<CatalogNumberMatchDto>());
        }

        if (maxCount < 1 || maxCount > SearchConstants.MaxOrganizationCount)
        {
            return BadRequest(new { error = $"maxCount must be between 1 and {SearchConstants.MaxOrganizationCount}." });
        }

        var result = await _lookupService.SearchCatalogNumbersAsync(search, maxCount, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns all taxon groups, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("TaxonGroups")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<TaxonGroupDto>>> GetTaxonGroups(CancellationToken cancellationToken = default)
    {
        var taxonGroups = await _lookupService.GetTaxonGroupsAsync(cancellationToken);
        return Ok(taxonGroups);
    }

    /// <summary>
    /// Returns all behaviors, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("Behaviors")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<BehaviorDto>>> GetBehaviors(CancellationToken cancellationToken = default)
    {
        var behaviors = await _lookupService.GetBehaviorsAsync(cancellationToken);
        return Ok(behaviors);
    }

    /// <summary>
    /// Returns all basis of record types, intended for populating filter dropdowns.
    /// </summary>
    [HttpGet("BasisOfRecords")]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<BasisOfRecordDto>>> GetBasisOfRecords(CancellationToken cancellationToken = default)
    {
        var basisOfRecords = await _lookupService.GetBasisOfRecordsAsync(cancellationToken);
        return Ok(basisOfRecords);
    }

    /// <summary>
    /// Returnerer direkte barn i taksonomien for en gitt forelder-taxon.
    /// Uten parentTaxonId returneres rotnodene (kingdom-nivå).
    /// Data hentes fra minne — ingen databasekall per request.
    /// </summary>
    [HttpGet("TaxonTree")]
    [Produces("application/json")]
    public ActionResult<List<TaxonTreeNodeDto>> GetTaxonTree([FromQuery] int? parentTaxonId = null, CancellationToken cancellationToken = default)
    {
        var children = _taxonHierarchy.GetChildren(parentTaxonId);
        return Ok(children);
    }

    /// <summary>
    /// Returnerer foreldrekjeden for hvert oppgitt taxonId.
    /// Brukes av frontend til å avgjøre indeterminate/checked-tilstand i taxon-treet
    /// for taxa valgt utenfor treet (f.eks. via artsøk).
    /// Data hentes fra minne — ingen databasekall per request.
    /// </summary>
    [HttpGet("TaxonAncestry")]
    [Produces("application/json")]
    public ActionResult<List<TaxonAncestryDto>> GetTaxonAncestry([FromQuery] int[] taxonIds, CancellationToken cancellationToken = default)
    {
        // Begrens input slik at endepunktet ikke kan misbrukes med svært store spørringer
        var ancestries = _taxonHierarchy.GetAncestries(taxonIds.Distinct().Take(MaxTaxonIdsPerRequest));
        return Ok(ancestries);
    }

    private const int MaxTaxonIdsPerRequest = 100;
}

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class MapLayerController : ControllerBase
{
    private readonly IMapLayerService _mapLayerService;

    public MapLayerController(IMapLayerService mapLayerService)
    {
        _mapLayerService = mapLayerService ?? throw new ArgumentNullException(nameof(mapLayerService));
    }

    /// <summary>
    /// Returns all map layers.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IEnumerable<MapLayerDto>>> GetMapLayers(CancellationToken cancellationToken = default)
    {
        var mapLayers = await _mapLayerService.GetMapLayersAsync(cancellationToken);
        return Ok(mapLayers);
    }
}

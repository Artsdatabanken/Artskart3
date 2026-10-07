using System.Security.Cryptography;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/observations")]
public sealed class ObservationsController(
    IObservationDetailsService detailsService,
    IObservationMapService maps,
    ILogger<ObservationsController> logger) : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<ObservationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ObservationDetailDto>> Get(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { error = "Observation ID must be positive." });
        var detail = await detailsService.GetAsync(id, cancellationToken);
        return detail is null ? NotFound(new { error = "Observation not found." }) : Ok(detail);
    }

    [HttpGet("{id:int}/media/{mediaId:int}/image")]
    [BffApiSkipAntiforgery] // Public, read-only images must work in an img element without custom headers.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Image(int id, int mediaId, CancellationToken cancellationToken)
    {
        if (id <= 0 || mediaId <= 0) return BadRequest(new { error = "Observation and media IDs must be positive." });
        var image = await detailsService.GetImageAsync(id, mediaId, cancellationToken);
        return image is null ? NotFound(new { error = "Image not available." }) : CachedImage(image.Bytes, image.ContentType);
    }

    [HttpGet("{id:int}/maps/{view}")]
    [BffApiSkipAntiforgery]
    [EnableRateLimiting("observation-maps")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Map(int id, string view, CancellationToken cancellationToken)
    {
        if (id <= 0 || view is not ("local" or "overview")) return BadRequest(new { error = "Invalid observation or map preset." });
        var observation = await detailsService.GetAsync(id, cancellationToken);
        if (observation is null) return NotFound(new { error = "Observation not found." });
        if (observation.Point is null) return UnprocessableEntity(new { error = "Public coordinates are not available." });
        try
        {
            return CachedImage(await maps.RenderAsync(observation.Point, view == "overview", cancellationToken), "image/png");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Map provider timed out for observation {ObservationId}", id);
            return StatusCode(504, new { error = "Map provider timed out. Please retry." });
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            logger.LogWarning(ex, "Map provider failed for observation {ObservationId}", id);
            return StatusCode(502, new { error = "Map is temporarily unavailable. Please retry." });
        }
    }

    private IActionResult CachedImage(byte[] bytes, string contentType)
    {
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes))}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public,max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (Request.GetTypedHeaders().IfNoneMatch?.Any(value => value.Tag == etag) == true) return StatusCode(304);
        return File(bytes, contentType);
    }
}

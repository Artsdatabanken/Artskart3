using System.Security.Cryptography;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/observations")]
public sealed class ObservationsController(
    IObservationDetailsService detailsService,
    IObservationMapService maps,
    IObservationAnnotationService annotations,
    ILogger<ObservationsController> logger) : ControllerBase
{
    // Kort, så et endret eller nylig maskert funn ikke vises med gammelt bilde. ETag-en gjør
    // revalideringen etterpå billig.
    private const int MaxAgeSeconds = 300;

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

    [HttpGet("by-occurrence-id")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<int>> FindByOccurrenceId([FromQuery] string? occurrenceId, CancellationToken cancellationToken)
    {
        var value = occurrenceId?.Trim();
        if (string.IsNullOrEmpty(value)) return BadRequest(new { error = "Occurrence ID is required." });
        var id = await detailsService.FindIdByOccurrenceIdAsync(value, cancellationToken);
        return id is null ? NotFound(new { error = "Observation not found." }) : Ok(id.Value);
    }

    [HttpGet("{id:int}/annotations")]
    [EnableRateLimiting("observation-annotations")]
    [ProducesResponseType<List<ObservationAnnotationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<List<ObservationAnnotationDto>>> Annotations(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { error = "Observation ID must be positive." });
        try
        {
            var result = await annotations.GetAsync(id, cancellationToken);
            return result is null ? NotFound(new { error = "Observation not found." }) : Ok(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Annotation service timed out for observation {ObservationId}", id);
            return StatusCode(504, new { error = "Annotation service timed out. Please retry." });
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            logger.LogWarning(ex, "Annotation service failed for observation {ObservationId}", id);
            return StatusCode(502, new { error = "Annotations are temporarily unavailable. Please retry." });
        }
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Map(int id, string view, CancellationToken cancellationToken)
    {
        if (id <= 0 || view is not ("local" or "overview")) return BadRequest(new { error = "Invalid observation or map preset." });
        var subject = await detailsService.GetMapSubjectAsync(id, cancellationToken);
        if (subject is null) return NotFound(new { error = "Observation not found." });
        if (subject.Point is null) return UnprocessableEntity(new { error = "Public coordinates are not available." });
        var overview = view == "overview";
        // Svak, fordi den regnes ut fra det bildet tegnes av og ikke fra bytene.
        var etag = new EntityTagHeaderValue($"\"{maps.Fingerprint(subject, overview)}\"", isWeak: true);
        if (HasCurrentCopy(etag))
        {
            SetCacheHeaders(etag);
            return StatusCode(304);
        }
        try
        {
            var map = await maps.RenderAsync(subject, overview, cancellationToken);
            SetCacheHeaders(etag);
            return File(map.Bytes, map.ContentType);
        }
        catch (ObservationMapBusyException)
        {
            logger.LogWarning("Observation map render queue is full for observation {ObservationId}", id);
            return StatusCode(429, new { error = "Too many map requests. Please retry." });
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
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(SHA256.HashData(bytes))}\"");
        SetCacheHeaders(etag);
        return HasCurrentCopy(etag) ? StatusCode(304) : File(bytes, contentType);
    }

    // If-None-Match sammenlignes alltid svakt (RFC 9110).
    private bool HasCurrentCopy(EntityTagHeaderValue etag) =>
        Request.GetTypedHeaders().IfNoneMatch?.Any(value => value.Compare(etag, useStrongComparison: false)) == true;

    private void SetCacheHeaders(EntityTagHeaderValue etag)
    {
        Response.Headers.ETag = etag.ToString();
        Response.Headers.CacheControl = $"public,max-age={MaxAgeSeconds}";
        Response.Headers.XContentTypeOptions = "nosniff";
    }
}

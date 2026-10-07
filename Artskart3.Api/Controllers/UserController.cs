using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserController(IUserService userService, ISavedFilterService savedFilterService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetCurrentUser(CancellationToken cancellationToken = default)
    {
        var subClaim = User.Claims.FirstOrDefault(x => x.Type == "sub");
        if (subClaim == null) return BadRequest("Missing 'sub' claim");
        if (!Guid.TryParse(subClaim.Value, out var userId)) return BadRequest("Invalid or missing userId");
        var user = await userService.GetCurrentUser(userId, cancellationToken);
        if (user == null) return NotFound("User not found");
        return Ok(user);
    }

    [HttpGet("SavedFilters")]
    public async Task<ActionResult<List<SavedFilterDto>>> GetSavedFilters(CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        return Ok(await savedFilterService.GetUserFiltersAsync(userId, cancellationToken));
    }

    [HttpPost("SavedFilters")]
    public async Task<ActionResult<SavedFilterDto>> CreateSavedFilter([FromBody] CreateSavedFilterRequestDto request, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        return Ok(await savedFilterService.CreateAsync(userId, request, cancellationToken));
    }

    [HttpDelete("SavedFilters/{id:guid}")]
    public async Task<ActionResult> DeleteSavedFilter(Guid id, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var deleted = await savedFilterService.DeleteAsync(id, userId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("SavedFilters/{id:guid}/default")]
    public async Task<ActionResult> SetDefaultSavedFilter(Guid id, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var updated = await savedFilterService.SetDefaultAsync(id, userId, true, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("SavedFilters/{id:guid}/default")]
    public async Task<ActionResult> ClearDefaultSavedFilter(Guid id, CancellationToken cancellationToken)
    {
        var error = TryGetUserId(out var userId);
        if (error != null) return error;

        var updated = await savedFilterService.SetDefaultAsync(id, userId, false, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private ActionResult? TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return Unauthorized(new { error = "Bruker mangler 'sub'-claim." });

        if (!Guid.TryParse(sub, out userId))
            return Unauthorized(new { error = "Bruker har ugyldig 'sub'-claim." });

        return null;
    }
}

using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserController(IUserService userService) : ControllerBase
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
}

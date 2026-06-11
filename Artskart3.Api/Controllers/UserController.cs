using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetCurrentUser()
    {
        var sub = User.Claims.First(x => x.Type == "sub").Value;
        if (Guid.TryParse(sub, out var userId)) return BadRequest("Invalid or missing userId");
        var user = await userService.GetCurrentUser(userId);
        if (user == null) return NotFound("User not found");
        return Ok(user);
    }
}
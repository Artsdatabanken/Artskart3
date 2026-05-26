using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IUserService userService) : ControllerBase
{
    public async Task<User> GetCurrentUser()
    {
        var userId = User.Claims.First(x => x.Type == "sub").Value;
        return await userService.GetCurrentUser(Guid.Parse(userId));
    }
}
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetCurrentUser(Guid userId);
    Task<User> GetOrCreateUser(User user);
}
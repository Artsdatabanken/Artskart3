using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IUserService
{
    Task<User> GetCurrentUser(Guid userId);
    Task<User> GetOrCreateUser(User user);
}
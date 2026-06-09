using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface IUserRepository
{
    Task<User?> GetUserById(Guid id);
    Task<User> CreateUser(User user);
}
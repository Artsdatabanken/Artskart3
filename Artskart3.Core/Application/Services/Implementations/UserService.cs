using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class UserService(IUserRepository userRepository) : IUserService
{
    public async Task<User> GetCurrentUser(Guid userId)
    {
        try
        {
            var user = await userRepository.GetUserById(userId);
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Error getting user");
        }
    }
}
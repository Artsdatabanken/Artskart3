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
            return user ?? throw new Exception("User not found");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Error getting user");
        }
    }

    public async Task<User> GetOrCreateUser(User user)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(user);
            var existingUser = await userRepository.GetUserById(user.Id);
            if (existingUser != null) return existingUser;
            var newUser = await userRepository.CreateUser(user);
            return newUser;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Error creating user");
        }
    }
}
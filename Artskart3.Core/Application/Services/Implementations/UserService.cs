using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Core.Application.Services.Implementations;

public class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto?> GetCurrentUser(Guid userId)
    {
        try
        {
            var user = await userRepository.GetUserById(userId);
            if (user == null) return null;
            var userDto = new UserDto
            {
                Name = user?.Name,
                Email = user?.Email
            };
            return userDto;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting user");
            throw new Exception("Error getting user", e);
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
            logger.LogError(e, "Error creating user");
            throw new Exception("Error creating user", e);
        }
    }
}
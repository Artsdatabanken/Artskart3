using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Core.Application.Services.Implementations;

public class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await userRepository.GetUserById(userId, cancellationToken);
            if (user == null) return null;
            var userDto = new UserDto
            {
                Name = user.Name,
                Email = user.Email
            };
            return userDto;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Error getting user");
            throw new ApplicationException("Error getting user", e);
        }
    }

    public async Task<User> GetOrCreateUser(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(user);
            var existingUser = await userRepository.GetUserById(user.Id, cancellationToken);
            if (existingUser == null)
                return await userRepository.CreateUser(user, cancellationToken);

            return await RefreshProfileAsync(existingUser, user, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException and not ArgumentNullException)
        {
            logger.LogError(e, "Error creating user");
            throw new ApplicationException("Error creating user", e);
        }
    }

    private async Task<User> RefreshProfileAsync(User existingUser, User fromIdentityProvider, CancellationToken cancellationToken)
    {
        // Tomme verdier skal aldri overskrive lagrede.
        var name = string.IsNullOrWhiteSpace(fromIdentityProvider.Name)
            ? existingUser.Name
            : fromIdentityProvider.Name;

        var email = string.IsNullOrWhiteSpace(fromIdentityProvider.Email)
            ? existingUser.Email
            : fromIdentityProvider.Email;

        // Skriv kun når noe faktisk er endret.
        var unchanged = string.Equals(existingUser.Name, name, StringComparison.Ordinal)
                     && string.Equals(existingUser.Email, email, StringComparison.Ordinal);

        if (unchanged) return existingUser;

        logger.LogInformation(
            "Oppdaterer profildata for bruker {UserId} etter endring hos identitetstjenesten",
            existingUser.Id);

        existingUser.Name = name;
        existingUser.Email = email;
        existingUser.UpdatedAt = DateTime.UtcNow;

        return await userRepository.UpdateUser(existingUser, cancellationToken);
    }
}

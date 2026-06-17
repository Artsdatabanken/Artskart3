using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class UserRepository(IArtsKartDbContext context, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await context.Set<User>().FindAsync([id], cancellationToken);
            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting user");
            throw new Exception("Error getting user", e);
        }
    }

    public async Task<User> CreateUser(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(user);
            context.Set<User>().Add(user);
            await context.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating user");
            throw new Exception("Error creating user", e);
        }
    }
}

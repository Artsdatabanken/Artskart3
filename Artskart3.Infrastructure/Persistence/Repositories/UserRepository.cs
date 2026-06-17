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
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Feil ved henting av bruker");
            throw new ApplicationException("Feil ved henting av bruker", e);
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
        catch (Exception e) when (e is not OperationCanceledException and not ArgumentNullException)
        {
            logger.LogError(e, "Feil ved opprettelse av bruker");
            throw new ApplicationException("Feil ved opprettelse av bruker", e);
        }
    }
}

using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class UserRepository(IArtsKartDbContext context, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<User> GetUserById(Guid id)
    {
        try
        {
            var user = await context.Set<User>().FindAsync(id) ?? throw new Exception("User not found");
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
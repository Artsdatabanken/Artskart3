namespace Artskart3.Core.Application.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public interface IArtsKartDbContext
{
    DatabaseFacade Database { get; }
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    DbSet<TEntity> Set<TEntity>(string name) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

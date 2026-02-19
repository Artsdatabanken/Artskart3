namespace Artskart3.Infrastructure.Persistence.Repositories
{
    using Microsoft.EntityFrameworkCore;
    public interface IArtsKartDbContext
    {
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

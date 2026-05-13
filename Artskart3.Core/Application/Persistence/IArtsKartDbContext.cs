namespace Artskart3.Core.Application.Persistence
{
    using Microsoft.EntityFrameworkCore;

    public interface IArtsKartDbContext
    {
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

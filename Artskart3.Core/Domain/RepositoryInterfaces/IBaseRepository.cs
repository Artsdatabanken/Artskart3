namespace Artskart3.Core.Domain.RepositoryInterfaces
{
    using Artskart3.Core.Domain.Entities;
    using System.Linq.Expressions;
    public interface IBaseRepository<TEntity> where TEntity : BaseEntity
    {
        Task<TEntity?> GetByIdAsync(int id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity> AddAsync(TEntity entity);
        Task AddRangeAsync(IEnumerable<TEntity> entities);
        Task<TEntity> UpdateAsync(TEntity entity);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<int> CountAsync();
        Task<IEnumerable<TEntity>> GetPagedAsync(int skip, int take);
    }
}

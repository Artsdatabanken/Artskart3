namespace Artskart3.Infrastructure.Persistence.Repositories
{
    using Artskart3.Core.Domain.Entities;
    using Artskart3.Core.Domain.RepositoryInterfaces;
    using Microsoft.EntityFrameworkCore;
    using System.Linq.Expressions;
    public abstract class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : BaseEntity
    {
        protected readonly IArtsKartDbContext _context;
        public BaseRepository(IArtsKartDbContext context)
        {
            _context = context;
        }
        public virtual async Task<TEntity?> GetByIdAsync(int id)
        {
            return await _context.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }
        public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return await _context.Set<TEntity>().Where(e => !e.IsDeleted).ToListAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _context.Set<TEntity>().Where(e => !e.IsDeleted).Where(predicate).ToListAsync();
        }
        public virtual async Task<TEntity> AddAsync(TEntity entity)
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.Set<TEntity>().AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            await _context.Set<TEntity>().AddRangeAsync(entityList);
            await _context.SaveChangesAsync();
        }
        public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _context.Set<TEntity>().Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        public virtual async Task<bool> DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
                return false;

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            _context.Set<TEntity>().Update(entity);
            await _context.SaveChangesAsync();
            return true;
        }
        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await _context.Set<TEntity>().AnyAsync(e => e.Id == id && !e.IsDeleted);
        }
        public virtual async Task<int> CountAsync()
        {
            return await _context.Set<TEntity>().Where(e => !e.IsDeleted).CountAsync();
        }
        public virtual async Task<IEnumerable<TEntity>> GetPagedAsync(int skip, int take)
        {
            return await _context.Set<TEntity>()
                .Where(e => !e.IsDeleted)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
    }
}

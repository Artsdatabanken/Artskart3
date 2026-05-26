using Artskart3.Import.Core.Domain.Entities;
using Artskart3.Import.Core.Domain.RepositoryInterfaces;
using Artskart3.Import.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Import.Infrastructure.Persistence.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly ArtskartImportDbContext _context;

    public DataSourceRepository(ArtskartImportDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DataSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DataSources
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DataSource?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.DataSources
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<DataSource> AddAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        dataSource.CreatedAt = DateTime.UtcNow;
        dataSource.UpdatedAt = DateTime.UtcNow;
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync(cancellationToken);
        return dataSource;
    }

    public async Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        dataSource.UpdatedAt = DateTime.UtcNow;
        _context.DataSources.Update(dataSource);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var dataSource = await GetByIdAsync(id, cancellationToken);
        if (dataSource is null) return;

        dataSource.IsDeleted = true;
        dataSource.DeletedAt = DateTime.UtcNow;
        dataSource.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}

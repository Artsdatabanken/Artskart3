using Artskart3.Import.Core.Domain.Entities;

namespace Artskart3.Import.Core.Domain.RepositoryInterfaces;

public interface IDataSourceRepository
{
    Task<IEnumerable<DataSource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSource?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DataSource> AddAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

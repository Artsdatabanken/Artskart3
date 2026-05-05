using Artskart3.Import.Core.Domain.Entities;
using Artskart3.Import.Core.Domain.Enums;

namespace Artskart3.Import.Core.Domain.RepositoryInterfaces;

public interface IGbifDatasetDiscoveryRepository
{
    Task<IEnumerable<GbifDatasetDiscovery>> GetAllAsync();
    Task<IEnumerable<GbifDatasetDiscovery>> GetByStatusAsync(DiscoveryStatus status);
    Task<GbifDatasetDiscovery?> GetByIdAsync(int id);
    Task<GbifDatasetDiscovery?> GetByGbifIdAsync(string gbifId);
    Task AddAsync(GbifDatasetDiscovery discovery);
    Task UpdateAsync(GbifDatasetDiscovery discovery);
}

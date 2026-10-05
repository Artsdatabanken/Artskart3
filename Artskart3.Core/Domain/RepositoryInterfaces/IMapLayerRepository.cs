using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface IMapLayerRepository
{
    Task<IEnumerable<MapLayerDto>> GetMapLayersAsync(CancellationToken cancellationToken = default);
}

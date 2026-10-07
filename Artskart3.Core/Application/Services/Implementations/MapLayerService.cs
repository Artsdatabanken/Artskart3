using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class MapLayerService : IMapLayerService
{
    private readonly IMapLayerRepository _mapLayerRepository;

    public MapLayerService(IMapLayerRepository mapLayerRepository)
    {
        _mapLayerRepository = mapLayerRepository ?? throw new ArgumentNullException(nameof(mapLayerRepository));
    }

    public Task<IEnumerable<MapLayerDto>> GetMapLayersAsync(CancellationToken cancellationToken = default)
    {
        return _mapLayerRepository.GetMapLayersAsync(cancellationToken);
    }
}

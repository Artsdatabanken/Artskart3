using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IMapLayerService
{
    Task<IEnumerable<MapLayerDto>> GetMapLayersAsync(CancellationToken cancellationToken = default);
}

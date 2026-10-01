using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationMapService
{
    Task<byte[]> RenderAsync(ObservationPointDto point, bool overview, CancellationToken cancellationToken);
}

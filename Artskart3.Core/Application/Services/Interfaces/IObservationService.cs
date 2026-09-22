
using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> GetObservationDetails(int locationId, int observationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ObservationListInfoDto>> GetObservationsByLocations(ObservationLocationRequestDto request,  CancellationToken cancellationToken = default);
}

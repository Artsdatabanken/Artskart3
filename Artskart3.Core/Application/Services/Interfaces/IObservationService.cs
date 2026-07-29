
using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationService
{
    Task<ObservationDto> GetObservationDetails(int locationId, int observationId);
    Task<IEnumerable<ObservationListInfoDto>> GetObservationsByLocations(IEnumerable<int> locationIds);
}

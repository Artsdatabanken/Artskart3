using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface IObservationRepository
{
    Task<ObservationDto> GetObservationDetails(int locationId, int observationId);
    Task<IEnumerable<ObservationListInfoDto>> GetObservationByLocations(IEnumerable<int> locationIds);
}

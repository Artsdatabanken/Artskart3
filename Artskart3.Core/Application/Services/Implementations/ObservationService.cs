using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class ObservationService(IObservationRepository observationService) : IObservationService
{
    public async Task<ObservationDto> GetObservationDetails(int locationId, int observationId, CancellationToken cancellationToken = default)
    {
        ObservationDto observation = await observationService.GetObservationDetails(locationId, observationId, cancellationToken);
        return observation;
    }

    public async Task<IEnumerable<ObservationListInfoDto>> GetObservationsByLocations(IEnumerable<int> locationIds, CancellationToken cancellationToken = default)
    {
        IEnumerable<ObservationListInfoDto> observations = await observationService.GetObservationByLocations(locationIds, cancellationToken);
        return observations;
    }
}

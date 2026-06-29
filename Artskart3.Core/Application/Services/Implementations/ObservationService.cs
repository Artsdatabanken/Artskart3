using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class ObservationService(IObservationRepository observationService) : IObservationService
{
    public async Task<ObservationDto> GetObservationDetails(int locationId, int observationId)
    {
        ObservationDto observation = await observationService.GetObservationDetails(locationId, observationId);
        return observation;
    }

    public async Task<IEnumerable<ObservationDto>> GetObservationsByLocation(IEnumerable<int> locationIds)
    {
        IEnumerable<ObservationDto> observations = await observationService.GetObservationByLocation(locationIds);
        return observations;
    }
}

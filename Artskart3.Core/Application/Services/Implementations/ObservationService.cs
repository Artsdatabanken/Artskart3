using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class ObservationService(IObservationRepository observationService) : IObservationService
{
    public async Task<Observation> GetObservationDetails(int locationId, int observationId)
    {
        Observation observation = await observationService.GetObservationDetails(locationId, observationId);
        return observation;
    }

    public async Task<IEnumerable<Observation>> GetObservationsByLocation(int locationId)
    {
        IEnumerable<Observation> observations = await observationService.GetObservationByLocation(locationId);
        return observations;
    }
}

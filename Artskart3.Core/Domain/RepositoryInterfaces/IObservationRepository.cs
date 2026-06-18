using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface IObservationRepository
{
    Task<Observation> GetObservationDetails(int locationId, int observationId);
    Task<IEnumerable<Observation>> GetObservationByLocation(int locationId);
}

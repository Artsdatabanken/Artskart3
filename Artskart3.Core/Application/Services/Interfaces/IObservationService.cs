
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationService
{
    Task<Observation> GetObservationDetails(int locationId, int observationId);
    Task<IEnumerable<Observation>> GetObservationsByLocation(int locationId);
}

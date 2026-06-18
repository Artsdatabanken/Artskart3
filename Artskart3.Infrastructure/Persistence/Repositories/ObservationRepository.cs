using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class ObservationRepository(IArtsKartDbContext context, ILogger<ObservationRepository> logger) : IObservationRepository
{
    public async Task<Observation> GetObservationDetails(int locationId, int observationId)
    {
        try
        {
            Observation observationDetails = await context.Set<Observation>()
                .Where(o => (o.LocationId == locationId) && (o.Id == observationId))
                .FirstAsync();
            return observationDetails;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while fetching observation details.");
            throw;
        }
    }

    public async Task<IEnumerable<Observation>> GetObservationByLocation(int locationId)
    {
        try
        {
            IEnumerable<Observation> observations = await context.Set<Observation>()
                .Where(o => o.LocationId == locationId)
                .ToListAsync();

            return observations;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured while fetching observations by locations");
            throw;
        }
    }
}

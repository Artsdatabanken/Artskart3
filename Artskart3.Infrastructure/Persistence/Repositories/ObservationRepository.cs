using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class ObservationRepository(IArtsKartDbContext context, ILogger<ObservationRepository> logger) : IObservationRepository
{
    public async Task<ObservationDto> GetObservationDetails(int locationId, int observationId)
    {
        try
        {
            Observation observationDetails = await context.Set<Observation>()
                .Where(o => (o.LocationId == locationId) && (o.Id == observationId))
                .FirstAsync();
            var observationDto = new ObservationDto
            {
                Id = observationDetails.Id,
                PreferredPopularName = observationDetails.MatchedScientificName.ScientificName,
                Author = observationDetails.MatchedScientificName.ScientificNameAuthorship,
            };
            return observationDto;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while fetching observation details.");
            throw;
        }
    }

    public async Task<IEnumerable<ObservationDto>> GetObservationByLocation(int locationId)
    {
        try
        {
            IEnumerable<Observation> observations = await context.Set<Observation>()
                .Include(o => o.Taxon)
                .Where(o => o.LocationId == locationId)
                .ToListAsync();
            IEnumerable<ObservationDto> observationDto = observations.Select(o => new ObservationDto
            {
                Id = o.Id,
                PreferredPopularName = o.Taxon.PreferredPopularName ?? string.Empty,
                ScientificName = o.Taxon.ValidScientificName ?? string.Empty,
                TaxonGroupId = o.TaxonGroupId,
                Locality = o.LocationId.ToString(),
            });
            return observationDto;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured while fetching observations by locations");
            throw;
        }
    }
}

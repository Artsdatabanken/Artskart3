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
                .Include(o => o.Taxon)
                .Where(o => (o.LocationId == locationId) && (o.Id == observationId))
                .FirstAsync();
            var observationDto = new ObservationDto
            {
                Id = observationDetails.Id,
                PreferredPopularName = observationDetails.Taxon.PreferredPopularName,
                ScientificName = observationDetails.Taxon.ValidScientificName,
                Author = observationDetails.Taxon.ValidScientificNameAuthorship,
            };
            return observationDto;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while fetching observation details.");
            throw;
        }
    }

    public async Task<IEnumerable<ObservationListInfoDto>> GetObservationByLocations(IEnumerable<int> locationIds)
    {
        try
        {
            IEnumerable<ObservationListInfoDto> observationListInfoDtos = await context.Set<Observation>()
                .Where(o => o.LocationId.HasValue && locationIds.ToList().Contains(o.LocationId.Value))
                .Take(2500)
                .Select(o => new ObservationListInfoDto
                {
                    Id = o.Id,
                    PreferredPopularName = o.Taxon.PreferredPopularName,
                    ScientificName = o.Taxon.ValidScientificName,
                    DisplayName = (o.Taxon.PreferredPopularName ?? o.MatchedScientificName.ScientificName)
                        .Replace("<i>", "").Replace("</i>", ""),
                    Author = o.Taxon.ValidScientificNameAuthorship,
                    TaxonGroupId = o.TaxonGroupId,
                    TaxonGroupName = o.Taxon.TaxonGroup.Name,
                    LocationId = o.LocationId,
                    CategoryId = o.CategoryId,
                    CategoryName = o.Category != null ? o.Category.Name : string.Empty,
                    RegistrationType = o.Tags.Select(t => t.Name),
                    IdentifiedBy = o.ObservationDetail != null ? o.ObservationDetail.IdentifiedBy : string.Empty,
                })
                .ToListAsync();
            return observationListInfoDtos;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured while fetching observations by locations");
            throw;
        }
    }
}

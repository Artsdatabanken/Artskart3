using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class ObservationRepository(IArtsKartDbContext context) : IObservationRepository
{
    public async Task<ObservationDto> GetObservationDetails(int locationId, int observationId, CancellationToken cancellationToken = default)
    {
        Observation observationDetails = await context.Set<Observation>()
            .Include(o => o.Taxon)
            .Where(o => (o.LocationId == locationId) && (o.Id == observationId))
            .FirstAsync(cancellationToken);
        var observationDto = new ObservationDto
        {
            Id = observationDetails.Id,
            PreferredPopularName = observationDetails.Taxon.PreferredPopularName,
            ScientificName = observationDetails.Taxon.ValidScientificName,
            Author = observationDetails.Taxon.ValidScientificNameAuthorship,
        };
        return observationDto;
    }

    public async Task<IEnumerable<ObservationListInfoDto>> GetObservationByLocations(IEnumerable<int> locationIds,
        CancellationToken cancellationToken = default)
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
                Collector = o.ObservationDetail != null ? o.ObservationDetail.Collector : string.Empty,
            })
            .ToListAsync(cancellationToken);
        return observationListInfoDtos;

    }
}

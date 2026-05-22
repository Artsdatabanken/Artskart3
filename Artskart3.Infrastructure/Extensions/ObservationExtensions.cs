using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;


namespace Artskart3.Infrastructure.Extensions
{
    internal static class ObservationExtensions
    {
        internal static ObservationDto ToObervationDto(this Observation o)
        {
            return new ObservationDto()
            {
                Id = o.Id,
                PreferredPopularName = o.Taxon.PreferredPopularName,
                ScientificName = o.Taxon.ValidScientificName,
                Author = o.Taxon.ValidScientificNameAuthorship,
                // todo, sjekk om det alltid kun er en institusjon koblet til en observasjon (fant ingen i databasen)
                Institution = o.OrganizationRelations
                                .Where(x => x.Organization.OrganizationTypeId == (int)Core.Domain.Enums.OrganizationType.Institution)
                                .Select(x => x.Organization.Name).FirstOrDefault(),
                Locality = o.Location?.Locality,
                // todo, sjekk om det alltid kun er en kommune koblet til observasjon (fant ingen i databasen)
                MunicipalityId = o.Location?.Areas
                                .Where(x => x.IsCurrent = true && x.AreaTypeId == (int)Core.Domain.Enums.AreaType.Municipality)
                                .Select(x => x.Fid).FirstOrDefault(),
                TaxonGroupId = o.TaxonGroupId,
                CategoryId= o.CategoryId,
                DateTimeCollected = o.DateTimeCollected
            };
        }
    }
}

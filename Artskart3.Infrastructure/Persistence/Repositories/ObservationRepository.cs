using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Microsoft.EntityFrameworkCore;
using TagEnum = Artskart3.Core.Domain.Enums.Tag;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class ObservationRepository(IArtsKartDbContext context, ITaxonHierarchyService _taxonHierarchy, IAreaHierarchyService _areaHierarchy) : IObservationRepository
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

    public async Task<IEnumerable<ObservationListInfoDto>> GetObservationByLocations(
        ObservationLocationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        request.Filter ??= new ObservationSearchFilterDto();
        var query = context.Set<Observation>().AsNoTracking();
        query = ApplyCommonFilters(query, request.Filter);
        query = query.Where(o => o.LocationId.HasValue && request.Ids.Contains(o.LocationId.Value));

        IEnumerable<ObservationListInfoDto> observationListInfoDtos = await query.Select(o => new ObservationListInfoDto
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
        }).ToListAsync(cancellationToken);
        return observationListInfoDtos;
    }

    private IQueryable<Observation> ApplyCommonFilters(
        IQueryable<Observation> query, IObservationFilter filter)
    {
        if (filter.TaxonGroupIds?.Any() == true)
        {
            var taxonGroupIds = filter.TaxonGroupIds.ToList();
            query = query.Where(o => taxonGroupIds.Contains(o.TaxonGroupId));
        }

        if (filter.TaxonIds?.Any() == true)
        {
            // Hierarkisk filtrering via ObservationTaxonHierarchy — inkluderer alle etterkommere
            IQueryable<int>? combinedQuery = null;
            foreach (var taxonId in filter.TaxonIds)
            {
                var subquery = GetObservationIdsByTaxonHierarchy(taxonId);
                combinedQuery = combinedQuery == null ? subquery : combinedQuery.Union(subquery);
            }
            query = query.Where(o => combinedQuery!.Contains(o.Id));
        }

        if (filter.CategoryIds?.Any() == true)
        {
            var categoryIds = filter.CategoryIds.ToList();
            query = query.Where(o => o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value));
        }

        // Geografiske områdefiltre via ObservationEntityIndex (OR — observasjon i minst ett av områdene)
        var hasMunicipality = filter.MunicipalityIds?.Any() == true;
        var hasCounty = filter.CountyIds?.Any() == true;
        var hasRestricted = filter.RestrictedAreaIds?.Any() == true;
        var hasOcean = filter.OceanAreaIds?.Any() == true;

        if (hasMunicipality || hasCounty || hasRestricted || hasOcean)
        {
            var municipalityIds = _areaHierarchy.FidsToEntityIds(filter.MunicipalityIds);
            var countyIds = _areaHierarchy.FidsToEntityIds(filter.CountyIds);
            var restrictedIds = _areaHierarchy.RestrictedAreaFidsToEntityIds(filter.RestrictedAreaIds);
            var oceanIds = _areaHierarchy.FidsToEntityIds(filter.OceanAreaIds);

            query = query.Where(o => context.Set<ObservationEntityIndex>().Any(idx =>
                idx.ObservationId == o.Id && (
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.Municipality && municipalityIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.County && countyIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen && countyIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.RestrictedArea && restrictedIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.OceanArea && oceanIds.Contains(idx.EntityId))
                )));
        }

        // Institusjonsfilter (AND — separat fra geografiske filtre).
        // Denormalisert kolonne, ikke lenger self-join mot indekstabellen.
        if (filter.OrganizationIds?.Any() == true)
        {
            var orgIds = filter.OrganizationIds;
            query = query.Where(o => o.InstitutionOrgId.HasValue && orgIds.Contains(o.InstitutionOrgId.Value));
        }

        if (filter.BehaviorIds?.Any() == true)
        {
            query = query.Where(o => o.Behaviors.Any(b => filter.BehaviorIds.Contains(b.Id)));
        }

        if (filter.BasisOfRecordIds?.Any() == true)
        {
            var basisOfRecordIds = filter.BasisOfRecordIds.ToList();
            query = query.Where(o => basisOfRecordIds.Contains(o.BasisOfRecordId));
        }

        if (filter.RegistrationStatusId.HasValue)
        {
            switch (filter.RegistrationStatusId.Value)
            {
                case 1:
                    query = query.Where(o => !o.Tags.Any(t => t.Id == (int)TagEnum.Absent || t.Id == (int)TagEnum.NotRecovered));
                    break;
                case 2:
                    query = query.Where(o => o.Tags.Any(t => t.Id == (int)TagEnum.Absent));
                    break;
                case 3:
                    query = query.Where(o => o.Tags.Any(t => t.Id == (int)TagEnum.NotRecovered));
                    break;
            }
        }

        if (filter.CoordinatePrecision?.From.HasValue == true)
        {
            query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecision.From.Value);
        }

        if (filter.CoordinatePrecision?.To.HasValue == true)
        {
            query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecision.To.Value);
        }

        if (filter.Period?.From.HasValue == true)
        {
            var fromDate = new DateTime(filter.Period.From.Value, 1, 1);
            query = query.Where(o => o.DateTimeCollected >= fromDate);
        }

        if (filter.Period?.To.HasValue == true)
        {
            var toDate = new DateTime(filter.Period.To.Value, 12, 31, 23, 59, 59);
            query = query.Where(o => o.DateTimeCollected <= toDate);
        }

        // Prosjekt/datasett — semi-join mot ObservationProject. Egen tabell fordi
        // datasett ikke er 1:1: 745 066 observasjoner har flere enn ett.
        if (filter.ProjectOrgId.HasValue)
        {
            var projectOrgId = filter.ProjectOrgId.Value;
            query = query.Where(o => context.Set<ObservationProject>()
                .Any(d => d.ObservationId == o.Id && d.ProjectOrgId == projectOrgId));
        }

        // Samling — denormalisert kolonne. Frontend sender ID fra typeahead, så
        // strengsammenligningen mot CollectionCode er borte.
        if (filter.DatasetOrgId.HasValue)
        {
            var datasetOrgId = filter.DatasetOrgId.Value;
            query = query.Where(o => o.DatasetOrgId == datasetOrgId);
        }

        // Katalognummer løses opp til ObservationId-er av oppslagsendepunktet.
        // Her er det bare en PK-liste igjen.
        if (filter.ObservationIds?.Any() == true)
        {
            var observationIds = filter.ObservationIds;
            query = query.Where(o => observationIds.Contains(o.Id));
        }

        if (filter.WithImages.HasValue)
        {
            query = filter.WithImages.Value
                ? query.Where(o => o.MediaFiles.Any())
                : query.Where(o => !o.MediaFiles.Any());
        }

        if (filter.Period?.Months?.Any() == true)
        {
            var months = filter.Period.Months;
            query = query.Where(o => o.DateTimeCollected.HasValue && months.Contains(o.DateTimeCollected.Value.Month));
        }

        return query;
    }

    private IQueryable<int> GetObservationIdsByTaxonHierarchy(int taxonId) =>
        ObservationQueryBuilder.GetObservationIdsByTaxonHierarchy(context, _taxonHierarchy, taxonId);
}

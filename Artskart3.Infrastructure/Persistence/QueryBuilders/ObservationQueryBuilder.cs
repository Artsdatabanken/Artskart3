using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.QueryBuilders;

/// <summary>
/// Bygger opp IQueryable-filtre for observasjoner basert på ObservationSearchFilterDto.
/// Delt mellom søk-endepunktet og CSV-eksporten.
/// </summary>
public static class ObservationQueryBuilder
{
    private const string SqlWildcard = "%";

    public static IQueryable<Observation> ApplyFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        query = ApplyTextFilters(query, filter);
        query = ApplyDirectFilters(query, filter);
        query = ApplyAreaFilters(context, query, filter);
        query = ApplyRangeFilters(query, filter);

        return query;
    }

    private static IQueryable<Observation> ApplyTextFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (!string.IsNullOrEmpty(filter.PreferredPopularName))
        {
            var pattern = SqlWildcard + filter.PreferredPopularName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.Taxon.PreferredPopularName, pattern));
        }

        if (!string.IsNullOrEmpty(filter.ScientificName))
        {
            var pattern = SqlWildcard + filter.ScientificName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificName, pattern));
        }

        if (!string.IsNullOrEmpty(filter.Author))
        {
            var pattern = SqlWildcard + filter.Author.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificNameAuthorship, pattern));
        }

        return query;
    }

    private static IQueryable<Observation> ApplyDirectFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.TaxonGroupIds?.Any() == true)
            query = query.Where(o => filter.TaxonGroupIds.Contains(o.TaxonGroupId));

        if (filter.CategoryIds?.Any() == true)
            query = query.Where(o => o.CategoryId != null && filter.CategoryIds.Contains(o.CategoryId.Value));

        if (filter.BehaviorIds?.Any() == true)
            query = query.Where(o => o.Behaviors.Any(b => filter.BehaviorIds.Contains(b.Id)));

        if (filter.BasisOfRecordIds?.Any() == true)
            query = query.Where(o => filter.BasisOfRecordIds.Contains(o.BasisOfRecordId));

        return query;
    }

    private static IQueryable<Observation> ApplyAreaFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        var filterQueries = new List<IQueryable<Observation>>();

        if (filter.MunicipalityIds?.Any() == true)
            filterQueries.Add(query.Where(o => context.Set<ObservationAreaIndex>().Any(idx =>
                idx.ObservationId == o.Id && idx.AreaTypeId == 1 && filter.MunicipalityIds.Contains(idx.AreaFid))));

        if (filter.CountyIds?.Any() == true)
            filterQueries.Add(query.Where(o => context.Set<ObservationAreaIndex>().Any(idx =>
                idx.ObservationId == o.Id && idx.AreaTypeId == 2 && filter.CountyIds.Contains(idx.AreaFid))));

        if (filter.RestrictedAreaIds?.Any() == true)
            filterQueries.Add(query.Where(o => context.Set<ObservationAreaIndex>().Any(idx =>
                idx.ObservationId == o.Id && idx.AreaTypeId == 3 && filter.RestrictedAreaIds.Contains(idx.AreaFid))));

        if (filter.OceanAreaIds?.Any() == true)
            filterQueries.Add(query.Where(o => context.Set<ObservationAreaIndex>().Any(idx =>
                idx.ObservationId == o.Id && idx.AreaTypeId == 4 && filter.OceanAreaIds.Contains(idx.AreaFid))));

        if (filter.OrganizationIds?.Any() == true)
        {
            var institutionFids = filter.OrganizationIds.Select(id => id.ToString()).ToArray();
            filterQueries.Add(query.Where(o => context.Set<ObservationAreaIndex>().Any(idx =>
                idx.ObservationId == o.Id && idx.AreaTypeId == 5 && institutionFids.Contains(idx.AreaFid))));
        }

        if (filterQueries.Count > 0)
            query = filterQueries.Aggregate((a, b) => a.Union(b));

        return query;
    }

    private static IQueryable<Observation> ApplyRangeFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.CoordinatePrecision?.From.HasValue == true)
            query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecision.From.Value);

        if (filter.CoordinatePrecision?.To.HasValue == true)
            query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecision.To.Value);

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

        return query;
    }
}

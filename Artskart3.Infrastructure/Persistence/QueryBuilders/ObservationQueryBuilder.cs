using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
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
        // Område- og organisasjonsfiltre via ObservationEntityIndex-tabellen.
        // Alle entiteter bruker int EntityId — string-Fid-er konverteres til int før spørring.
        // Speiler logikken i SearchRepository slik at CSV-eksporten filtrerer identisk med søk.
        var hasMunicipality = filter.MunicipalityIds?.Any() == true;
        var hasCounty = filter.CountyIds?.Any() == true;
        var hasRestricted = filter.RestrictedAreaIds?.Any() == true;
        var hasOcean = filter.OceanAreaIds?.Any() == true;
        var hasOrg = filter.OrganizationIds?.Any() == true;

        if (hasMunicipality || hasCounty || hasRestricted || hasOcean || hasOrg)
        {
            var municipalityIds = ConvertFidsToInt(filter.MunicipalityIds);
            var countyIds = ConvertFidsToInt(filter.CountyIds);
            var restrictedIds = ConvertRestrictedAreaFidsToInt(filter.RestrictedAreaIds);
            var oceanIds = ConvertFidsToInt(filter.OceanAreaIds);
            var orgIds = filter.OrganizationIds ?? [];

            query = query.Where(o => context.Set<ObservationEntityIndex>().Any(idx =>
                idx.ObservationId == o.Id && (
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.Municipality && municipalityIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.County && countyIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.RestrictedArea && restrictedIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.OceanArea && oceanIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.Institution && orgIds.Contains(idx.EntityId))
                )));
        }

        return query;
    }

    /// <summary>
    /// Konverterer string-Fid-er til int ved å fjerne "_" (for historiske fylkes-Fid-er som "15_2017").
    /// </summary>
    private static int[] ConvertFidsToInt(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(fid => int.TryParse(fid.Replace("_", ""), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    /// <summary>
    /// Konverterer verneområde-Fid-er til int ved å fjerne "Naturbase VV"-prefiks.
    /// </summary>
    private static int[] ConvertRestrictedAreaFidsToInt(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(fid => int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
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

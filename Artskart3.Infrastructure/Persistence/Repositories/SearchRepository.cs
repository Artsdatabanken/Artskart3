using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artskart3.Infrastructure.Persistence.Repositories
{
    public class SearchRepository : ISearchRepository
    {
        private const int DefaultMaxSearchResults = 20;
        private const int MinValidMaxCount = 1;
        private const int MaxValidMaxCount = 1000;
        private const int DefaultMaxLocations = 1000;
        private const int MaxLocations = 1000;
        private const string SqlWildcard = "%";
        
        private readonly IArtsKartDbContext _context;
        private readonly ILogger<SearchRepository> _logger;

        public SearchRepository(IArtsKartDbContext context, ILogger<SearchRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        /// <summary>
        /// Searches for taxa by name using a three-level matching strategy:
        /// 1. Exact matches on scientific or common names
        /// 2. Starts-with matches
        /// 3. Contains matches
        /// Returns up to maxCount results from active taxa (not deleted and have observation data).
        /// </summary>
        public async Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = DefaultMaxSearchResults)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Enumerable.Empty<TaxonDto>();
                }

                if (maxCount < MinValidMaxCount || maxCount > MaxValidMaxCount)
                {
                    throw new ArgumentException(
                        $"Max count must be between {MinValidMaxCount} and {MaxValidMaxCount}.",
                        nameof(maxCount));
                }

                var searchTerm = name.Trim().ToLower();

                var matchingIds = GetExactMatches(searchTerm)
                    .Union(GetStartsWithMatches(searchTerm))
                    .Union(GetContainsMatches(searchTerm))
                    .Distinct()
                    .Take(maxCount);

                var result = await _context.Set<Taxon>()
                    .Where(t => matchingIds.Contains(t.Id))
                    .Select(t => new TaxonDto
                    {
                        Id = t.Id,
                        ExternalTaxonId = t.ExternalTaxonId,
                        ValidScientificName = t.ValidScientificName,
                        ValidScientificNameAuthorship = t.ValidScientificNameAuthorship,
                        PreferredPopularName = t.PreferredPopularName,
                        TaxonGroupId = t.TaxonGroupId,
                        TaxonRankId = t.TaxonRankId,
                        CumulativeObservationCount = t.CumulativeObservationCount,
                        ExistsInCountry = t.ExistsInCountry
                    })
                    .ToListAsync();

                return result;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument validation failed for taxon search with name: {Name}", name);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred during taxon search for name: {Name}", name);
                throw new ApplicationException("A database error occurred while searching taxa. Please try again later.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during taxon search for name: {Name}", name);
                throw new ApplicationException("An unexpected error occurred while searching taxa. Please contact support if the problem persists.", ex);
            }
        }
        private IQueryable<Taxon> GetActiveTaxa()
        {
            return _context.Set<Taxon>()
                .Where(t => !t.IsDeleted && (t.CumulativeObservationCount > 0 || t.ExistsInCountry));
        }
        private IQueryable<int> GetExactMatches(string searchTerm)
        {
            return GetActiveTaxa()
                .Where(t =>
                    t.TaxonNames.Any(tn => !tn.IsDeleted && tn.ScientificName.ToLower() == searchTerm) ||
                    t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted && tpn.Name.ToLower() == searchTerm)
                )
                .Select(t => t.Id);
        }
        private IQueryable<int> GetStartsWithMatches(string searchTerm)
        {
            var startsWithPattern = searchTerm + SqlWildcard;
            
            return GetActiveTaxa()
                .Where(t =>
                    t.TaxonNames.Any(tn => !tn.IsDeleted &&
                        EF.Functions.Like(tn.ScientificName.ToLower(), startsWithPattern))
                    ||
                    t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted &&
                        EF.Functions.Like(tpn.Name.ToLower(), startsWithPattern))
                )
                .Select(t => t.Id);
        }
        private IQueryable<int> GetContainsMatches(string searchTerm)
        {
            var containsPattern = SqlWildcard + searchTerm + SqlWildcard;
            
            return GetActiveTaxa()
                .Where(t =>
                    t.TaxonNames.Any(tn => !tn.IsDeleted &&
                        EF.Functions.Like(tn.ScientificName.ToLower(), containsPattern))
                    ||
                    t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted &&
                        EF.Functions.Like(tpn.Name.ToLower(), containsPattern))
                )
                .Select(t => t.Id);
        }
        /// <summary>
        /// Retrieves observation locations filtered by taxon group, collection, category, basis of record, and coordinate precision.
        /// Aggregates observation counts by location, sorted by count descending.
        /// Returns locations as an async enumerable with UTM Zone 33N coordinates (East/North) and metadata.
        /// </summary>
        public async IAsyncEnumerable<LocationModel> GetLocationsAsync(LocationSearchFilterDto? filter = null)
        {
            var locationModels = await FetchLocationModelsAsync(filter);

            foreach (var model in locationModels)
            {
                yield return model;
            }
        }

        private async Task<List<LocationModel>> FetchLocationModelsAsync(LocationSearchFilterDto? filter)
        {
            try
            {
                filter ??= new LocationSearchFilterDto();

                var query = BuildLocationsQuery(filter);
                var aggregatedData = await AggregateLocationObservations(query, filter.MaxResults);

                if (aggregatedData.Count == 0)
                    return [];

                var locationModels = await BuildLocationModels(aggregatedData);

                _logger.LogInformation("Location search completed successfully. Returned {LocationCount} locations", locationModels.Count);

                return locationModels;
            }
            catch (InvalidOperationException ex)
            {
                throw new ApplicationException("Failed to retrieve locations due to an invalid operation. Please verify your search parameters.", ex);
            }
            catch (DbUpdateException ex)
            {
                throw new ApplicationException("A database error occurred while retrieving locations. Please try again later.", ex);
            }
            catch (OperationCanceledException ex)
            {
                throw new ApplicationException("The location search operation was cancelled. Please try again.", ex);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An unexpected error occurred while retrieving locations. Please contact support if the problem persists.", ex);
            }
        }

        private IQueryable<Observation> BuildLocationsQuery(LocationSearchFilterDto filter)
        {
            var query = _context.Set<Observation>().AsNoTracking();

            if (filter.TaxonGroupIds?.Any() == true)
            {
                var taxonGroupIds = filter.TaxonGroupIds.ToList();
                query = query.Where(o => taxonGroupIds.Contains(o.TaxonGroupId));
            }

            if (filter.CollectionIds?.Any() == true)
            {
                var collectionIds = filter.CollectionIds.ToList();
                query = query.Where(o => o.InstitutionCode != null && collectionIds.Contains(o.InstitutionCode));
            }

            if (filter.Categories?.Any() == true)
            {
                var categories = filter.Categories.ToList();
                query = query.Where(o => o.CategoryId.HasValue && categories.Contains(o.CategoryId.Value));
            }

            if (filter.BasisOfRecords?.Any() == true)
            {
                var basisOfRecords = filter.BasisOfRecords.ToList();
                query = query.Where(o => basisOfRecords.Contains(o.BasisOfRecordId));
            }

            if (filter.CoordinatePrecisionFrom > 0)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecisionFrom);
            }

            if (filter.CoordinatePrecisionTo > 0)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecisionTo);
            }

            return query;
        }

        private async Task<List<AggregatedLocationData>> AggregateLocationObservations(
            IQueryable<Observation> query,
            int requestedMaxResults)
        {
            var maxResults = requestedMaxResults > 0 && requestedMaxResults <= MaxLocations
                ? requestedMaxResults
                : DefaultMaxLocations;

            return await query
                .Where(o => o.LocationId != null)
                .GroupBy(o => new { o.LocationId!.Value, o.TaxonId })
                .Select(g => new AggregatedLocationData
                {
                    LocationId = g.Key.Value,
                    TaxonId = g.Key.TaxonId,
                    ObservationCount = g.Count()
                })
                .OrderByDescending(x => x.ObservationCount)
                .Take(maxResults)
                .ToListAsync();
        }

        private async Task<List<LocationModel>> BuildLocationModels(List<AggregatedLocationData> aggregatedData)
        {
            var locationIds = aggregatedData.Select(x => x.LocationId).ToList();

            var locationLookup = await _context.Set<Location>()
                .Where(l => locationIds.Contains(l.Id))
                .AsNoTracking()
                .ToDictionaryAsync(l => l.Id);

            var locationModels = new List<LocationModel>(aggregatedData.Count);

            foreach (var item in aggregatedData)
            {
                if (locationLookup.TryGetValue(item.LocationId, out var location))
                {
                    locationModels.Add(MapLocationToModel(location, item));
                }
            }

            return locationModels;
        }
        private static LocationModel MapLocationToModel(Location location, AggregatedLocationData aggregatedData)
        {
            return new LocationModel
            {
                Id = location.Id,
                Locality = location.Locality ?? string.Empty,
                Latitude = location.Latitude ?? 0,
                Longitude = location.Longitude ?? 0,
                East = location.East,
                North = location.North,
                CoordinatePrecision = location.CoordinatePrecision,
                TaxonId = aggregatedData.TaxonId,
                ObservationCount = aggregatedData.ObservationCount
            };
        }

        private class AggregatedLocationData
        {
            public int LocationId { get; set; }
            public int? TaxonId { get; set; }
            public int ObservationCount { get; set; }
        }

        /// <summary>
        /// Retrieves all area markers (counties/municipalities) grouped by name with aggregated observation counts.
        /// Returns one polygon per area name in WKT POLYGON format in UTM Zone 33N.
        /// Area types: 1 = municipalities, 2 = counties.
        /// At lower zoom levels shows counties; at higher zoom levels shows municipalities.
        /// </summary>
        public async Task<IEnumerable<AreaMarkerDto>> GetObservationsByZoomLevelAsync(int zoomLevel)
        {
            try
            {
                var areas = await _context.Set<Area>()
                    .Where(a => a.ZoomLevel == zoomLevel)
                    .ToListAsync();

                return areas
                    .GroupBy(a => a.Name)
                    .Select(g => {
                        var firstArea = g.FirstOrDefault(a => a.WktPolygon != null) ?? g.First();
                        return new AreaMarkerDto
                        {
                            Id = g.Min(a => a.Id),
                            DocumentId = firstArea.DocumentId,
                            Fid = firstArea.Fid,
                            Name = g.Key,
                            AreaTypeId = firstArea.AreaTypeId,
                            ParentFid = firstArea.ParentFid,
                            ObservationCount = g.Sum(a => a.ObservationCount),
                            WktsPolygon = firstArea.WktPolygon?.AsText()
                        };
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving areas for zoom level: {ZoomLevel}", zoomLevel);
                throw new ApplicationException(
                    "An error occurred while retrieving areas. Please try again later.", ex);
            }
        }


    }
}
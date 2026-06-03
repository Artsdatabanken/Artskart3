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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Artskart3.Infrastructure.Persistence.Repositories
{
    public class SearchRepository : ISearchRepository
    {
        private const int DefaultMaxSearchResults = 20;
        private const int MinValidMaxCount = 1;
        private const int MaxValidMaxCount = 1000;
        private const int DefaultMaxLocations = 1000;
        private const int MaxLocations = 10000;
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
        public async Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = DefaultMaxSearchResults, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Enumerable.Empty<Taxon>();
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
                    .Include(t => t.TaxonNames)
                    .Include(t => t.TaxonPopularNames)
                    .ToListAsync(cancellationToken);

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
            catch (Exception ex) when (ex is not OperationCanceledException)
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


        public async Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default)
        {
            var query = _context.Set<Observation>()
                                .AsNoTracking();

            //Where clauses
            if (!string.IsNullOrEmpty(filter.PreferredPopularName))
            {
                var popularNamePattern = SqlWildcard + filter.PreferredPopularName + SqlWildcard;
                query = query.Where(o => EF.Functions.Like(o.Taxon.PreferredPopularName, popularNamePattern));
            }

            if (!string.IsNullOrEmpty(filter.ScientificName))
            {
                var scientificNamePattern = SqlWildcard + filter.ScientificName + SqlWildcard;
                query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificName, scientificNamePattern));
            }

            if (!string.IsNullOrEmpty(filter.Author))
            {
                var authorPattern = SqlWildcard + filter.Author + SqlWildcard;
                query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificNameAuthorship, authorPattern));
            }

            if (!string.IsNullOrEmpty(filter.Locality))
            {
                query = query.Where(o => EF.Functions.Like(o.Location.Locality, filter.Locality));
            }

            if (filter.TaxonGroupIds?.Any() == true)
            {
                query = query.Where(o => filter.TaxonGroupIds.Contains(o.TaxonGroupId));
            }

            if(filter.OrganizationIds?.Any() == true)
            {
                query = query.Where(o => o.OrganizationRelations
                                          .Any(x => x.Organization.OrganizationTypeId == (int)Core.Domain.Enums.OrganizationType.Institution 
                                               && filter.OrganizationIds.Contains(x.OrganizationId)));
            }

            if(filter.RisikokategoriIder?.Any() == true)
            {
                query = query.Where(o => o.CategoryId != null && filter.RisikokategoriIder.Contains(o.CategoryId.Value));
            }

            if(filter.MunicipalityIds?.Any() == true)
            {
                query = query.Where(o => o.Location != null && o.Location.Areas.Any(x => 
                    x.IsCurrent == true &&
                    x.AreaTypeId == (int)Core.Domain.Enums.AreaType.Municipality &&
                    filter.MunicipalityIds.Contains(x.Fid)));
            }

            if(filter.CountyIds?.Any() == true)
            {
                query = query.Where(o => o.Location != null && o.Location.Areas.Any(x =>
                    x.IsCurrent == true &&
                    x.AreaTypeId == (int)Core.Domain.Enums.AreaType.County &&
                    filter.CountyIds.Contains(x.Fid)));
            }

            if(filter.BehaviorIds?.Any() == true)
            {
                query = query.Where(o => o.Behaviors.Any(b => filter.BehaviorIds.Contains(b.Id)));
            }

            if(filter.BasisOfRecordIds?.Any() == true)
            {
                query = query.Where(o => filter.BasisOfRecordIds.Contains(o.BasisOfRecordId));
            }

            if(filter.CoordinatePrecision?.From.HasValue == true)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecision.From.Value);
            }

            if(filter.CoordinatePrecision?.To.HasValue == true)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecision.To.Value);
            }

            if(filter.Period?.From.HasValue == true)
            {
                var fromDate = new DateTime(filter.Period.From.Value, 1, 1);
                query = query.Where(o => o.DateTimeCollected >= fromDate);
            }

            if(filter.Period?.To.HasValue == true)
            {
                var toDate = new DateTime(filter.Period.To.Value, 12, 31, 23, 59, 59);
                query = query.Where(o => o.DateTimeCollected <= toDate);
            }

            query = query.OrderBy(o => o.Id);

            if(filter.IsPaginated)
            {
                var skip = (filter.PageNumber!.Value - 1) * filter.ResultsPerPage!.Value;
                if (skip > 0)
                {
                    query = query.Skip(skip);
                }
                // todo: fix so that *4 below is read from a constant somewhere
                query = query.Take(filter.ResultsPerPage!.Value * 4);
            }
            else
            {
                query = query.Take(DefaultMaxSearchResults);
            }

            return await query.Select(o => new ObservationDto
            {
                Id = o.Id,
                PreferredPopularName = o.Taxon.PreferredPopularName,
                ScientificName = o.Taxon.ValidScientificName,
                Author = o.Taxon.ValidScientificNameAuthorship,
                Institution = o.OrganizationRelations
                    .Where(x => x.Organization.OrganizationTypeId == (int)Core.Domain.Enums.OrganizationType.Institution)
                    .Select(x => x.Organization.Name)
                    .FirstOrDefault(),
                Locality = o.Location != null ? o.Location.Locality : null,
                MunicipalityId = o.Location != null
                    ? o.Location.Areas
                        .Where(x => x.IsCurrent == true && x.AreaTypeId == (int)Core.Domain.Enums.AreaType.Municipality)
                        .Select(x => x.Fid)
                        .FirstOrDefault()
                    : null,
                TaxonGroupId = o.TaxonGroupId,
                CategoryId = o.CategoryId,
                DateTimeCollected = o.DateTimeCollected,
                CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters
            }).ToListAsync(cancellationToken);
        }


        /// <summary>
        /// Retrieves observation locations filtered by taxon group, collection, category, basis of record, and coordinate precision.
        /// Aggregates observation counts by location, sorted by count descending.
        /// Returns locations as an async enumerable with UTM Zone 33N coordinates (East/North) and metadata.
        /// </summary>
        public async IAsyncEnumerable<LocationModel> GetLocationsAsync(LocationSearchFilterDto? filter = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var locationModels = await FetchLocationModelsAsync(filter, cancellationToken);

            foreach (var model in locationModels)
            {
                yield return model;
            }
        }


        private async Task<List<LocationModel>> FetchLocationModelsAsync(LocationSearchFilterDto? filter, CancellationToken cancellationToken = default)
        {
            try
            {
                filter ??= new LocationSearchFilterDto();

                // Hvorfor bruker vi ikke arrays i LocationSearchFilterDto?
                // Dette er noe vi styrer selv, så unødvendig å bruke kommaseparert liste for så å ha egen kode for å lage en liste av dem
                var taxonGroupIds = ParseIntList(filter.TaxonGroupIds);
                var categories = ParseIntList(filter.Categories);
                var basisOfRecords = ParseIntList(filter.BasisOfRecords);
                var collectionIds = ParseStringList(filter.CollectionIds);

                var query = _context.Set<Observation>()
                    .AsNoTracking();

                if (taxonGroupIds.Any())
                {
                    query = query.Where(o => taxonGroupIds.Contains(o.TaxonGroupId));
                }

                if (collectionIds.Any())
                {
                    query = query.Where(o => o.InstitutionCode != null && collectionIds.Contains(o.InstitutionCode));
                }

                if (categories.Any())
                {
                    query = query.Where(o => o.CategoryId.HasValue && categories.Contains(o.CategoryId.Value));
                }

                if (basisOfRecords.Any())
                {
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

                var maxResults = filter.MaxResults > 0 && filter.MaxResults <= MaxLocations ? filter.MaxResults : DefaultMaxLocations;

                var aggregatedData = await query
                    .Where(o => o.LocationId != null)
                    .GroupBy(o => o.LocationId!.Value)
                    .Select(g => new
                    {
                        LocationId = g.Key,
                        ObservationCount = g.Count()
                    })
                    .OrderByDescending(x => x.ObservationCount)
                    .Take(maxResults)
                    .ToListAsync(cancellationToken);

                if (aggregatedData.Count == 0)
                    return [];

                var locationIds = aggregatedData.Select(x => x.LocationId).ToList();

                var locationLookup = await _context.Set<Location>()
                    .Where(l => locationIds.Contains(l.Id))
                    .AsNoTracking()
                    .ToDictionaryAsync(l => l.Id, cancellationToken);

                var locationModels = new List<LocationModel>(aggregatedData.Count);

                foreach (var item in aggregatedData)
                {
                    if (locationLookup.TryGetValue(item.LocationId, out var location))
                    {
                        locationModels.Add(new LocationModel
                        {
                            Id = location.Id,
                            Locality = location.Locality ?? string.Empty,
                            Latitude = location.Latitude ?? 0,
                            Longitude = location.Longitude ?? 0,
                            East = location.East,
                            North = location.North,
                            CoordinatePrecision = location.CoordinatePrecision,
                            ObservationCount = item.ObservationCount
                        });
                    }
                }

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

        /// <summary>
        /// Retrieves all area markers (counties/municipalities) grouped by name with aggregated observation counts.
        /// Returns one centroid per area name in WKT POINT format: POINT(easting northing) in UTM Zone 33N.
        /// Area types: 1 = municipalities, 2 = counties.
        /// </summary>
        public async Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(int[] areaTypeIds, CancellationToken cancellationToken = default)
        {
            try
            {
                if (areaTypeIds.Length == 0)
                    return [];

                var areas = await _context.Set<Area>()
                    .Where(a => areaTypeIds.Contains(a.AreaTypeId))
                    .ToListAsync(cancellationToken);

                return areas
                    .GroupBy(a => a.Name)
                    .Select(g => new AreaMarkerDto
                    {
                        Id = g.Min(a => a.Id),
                        Name = g.Key,
                        AreaTypeId = g.First().AreaTypeId,
                        ObservationCount = g.Sum(a => a.ObservationCount),
                        Centroid = g.FirstOrDefault(a => a.WktPolygon != null)
                            ?.WktPolygon?.Centroid?.AsText()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving areas for type IDs: {AreaTypeIds}", 
                    string.Join(",", areaTypeIds));
                throw new ApplicationException(
                    "An error occurred while retrieving areas. Please try again later.", ex);
            }
        }

        private List<int> ParseIntList(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
                return new List<int>();

            return values
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => int.TryParse(s, out _))
                .Select(s => int.Parse(s))
                .ToList();
        }

        private List<string> ParseStringList(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
                return new List<string>();

            return values
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
    }
}
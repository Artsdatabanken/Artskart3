using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
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
        
        private readonly IArtsKartDbContext _context;

        public SearchRepository(IArtsKartDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public async Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = DefaultMaxSearchResults)
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
                .ToListAsync();

            return result;
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
            var startsWithPattern = searchTerm + "%";
            
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
            var containsPattern = "%" + searchTerm + "%";
            
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
        public async IAsyncEnumerable<LocationModel> GetLocationsAsync(LocationSearchFilterDto? filter = null)
        {
            filter ??= new LocationSearchFilterDto();

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

            if (filter.CoordinatePrecisionFrom > 0)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecisionFrom);
            }
            if (filter.CoordinatePrecisionTo > 0)
            {
                query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecisionTo);
            }

            var maxResults = filter.MaxResults > 0 && filter.MaxResults <= 10000 ? filter.MaxResults : 1000; //skal fikses results
            
            var aggregatedLocations = new List<(int LocationId, int ObservationCount)>();
            var locations = new List<Location>();

            try
            {
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
                    .ToListAsync();

                aggregatedLocations = aggregatedData.Select(x => (x.LocationId, x.ObservationCount)).ToList();

                if (aggregatedLocations.Any())
                {
                    var locationIds = aggregatedLocations.Select(x => x.LocationId).ToList();
                    locations = await _context.Set<Location>()
                        .Where(l => locationIds.Contains(l.Id))
                        .AsNoTracking()
                        .ToListAsync();
                }
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

            if (locations.Any())
            {
                var locationLookup = locations.ToDictionary(l => l.Id);
                foreach (var (locationId, observationCount) in aggregatedLocations)
                {
                    if (locationLookup.TryGetValue(locationId, out var location))
                    {
                        yield return new LocationModel
                        {
                            Id = location.Id,
                            Locality = location.Locality ?? string.Empty,
                            Latitude = location.Latitude ?? 0,
                            Longitude = location.Longitude ?? 0,
                            East = location.East,
                            North = location.North,
                            CoordinatePrecision = location.CoordinatePrecision,
                            ObservationCount = observationCount
                        };
                    }
                }
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
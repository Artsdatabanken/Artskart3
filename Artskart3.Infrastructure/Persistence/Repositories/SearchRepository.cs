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
    }
}
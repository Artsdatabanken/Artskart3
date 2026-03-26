using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Artskart3.Core.Application.Services.Implementations
{
    public class SearchService : ISearchService
    {
        private readonly ISearchRepository _searchRepository;
        public SearchService(ISearchRepository searchRepository)
        {
            _searchRepository = searchRepository;
        }
        public async Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20)
        {
           var alltaxons = await _searchRepository.GetTaxonsAsync(name,  maxCount);
           
           return alltaxons;
        }
    }
}
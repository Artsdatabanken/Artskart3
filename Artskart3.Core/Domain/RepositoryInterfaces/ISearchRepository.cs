using Artskart3.Core.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Artskart3.Core.Domain.RepositoryInterfaces
{
    public interface ISearchRepository
    {
        Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20);
    }
}
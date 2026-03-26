using Artskart3.Core.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ISearchService
    {
        Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20);
    }
}
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.RepositoryInterfaces
{
    public interface ISearchRepository
    {
        Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20);
        IAsyncEnumerable<LocationModel> GetLocationsAsync(LocationSearchFilterDto? filter = null);
        Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(params int[] areaTypeIds);
    }
}
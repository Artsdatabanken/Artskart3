using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ISearchService
    {
        Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20);
        Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null);
        Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(params int[] areaTypeIds);
    }
}
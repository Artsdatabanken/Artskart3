using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ISearchService
    {
        Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20);
        Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null);
        Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(params int[] areaTypeIds);
    }
}
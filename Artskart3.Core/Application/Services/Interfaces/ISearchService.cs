using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;

namespace Artskart3.Core.Application.Services.Interfaces
{
    public interface ISearchService
    {
        Task<IEnumerable<Taxon>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default);
        Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
        Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default);
        Task<IEnumerable<AreaMarkerDto>> GetAreasByTypeIdsAsync(int[] areaTypeIds, CancellationToken cancellationToken = default);
    }
}
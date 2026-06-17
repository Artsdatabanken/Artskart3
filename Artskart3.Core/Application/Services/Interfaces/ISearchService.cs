using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ISearchService
{
    Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default);
    Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default);

    //Todo Denne returnerer en liste med AreaMarkerDto, bør bytte navn
    Task<IEnumerable<AreaMarkerDto>> GetAreaMarkersAsync(int zoomLevel, CancellationToken cancellationToken = default);
}

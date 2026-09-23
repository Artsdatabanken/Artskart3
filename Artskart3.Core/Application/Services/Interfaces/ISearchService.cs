using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.BusinessModels;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ISearchService
{
    Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default);
    Task<List<LocationModel>> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default);

    Task<IEnumerable<AreaMarkerDto>> GetAreaMarkersAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ferdig serialisert og gzippet svar for det ufiltrerte områdesøket på zoomnivå 1 og 2.
    /// Gjelder kun den ufiltrerte formen — det er den eneste frontend ber om, og den
    /// eneste som er identisk mellom kall. Se <see cref="AreaMarkersPayloadDto"/>.
    /// </summary>
    Task<AreaMarkersPayloadDto> GetAreaMarkersPayloadAsync(int zoomLevel, CancellationToken cancellationToken = default);
    Task<IEnumerable<LocationPolygonDto>> GetLocationPolygonsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
    Task<AreaCountsResultDto> GetAreaCountsAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
}

using Artskart3.Core.Application.Converters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;

namespace Artskart3.Core.Application.Services.Implementations;

public class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepository;

    public SearchService(ISearchRepository searchRepository)
    {
        _searchRepository = searchRepository;
    }

    public async Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        filter = filter ?? new LocationSearchFilterDto();

        try
        {
            var locations = _searchRepository.GetLocationsAsync(filter, cancellationToken);
            return await GeoJsonConverter.LocationsToGeoJson(locations, StyleType.Unknown, filter.Epsg, cancellationToken);
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Feil ved henting av lokasjoner", ex);
        }
    }

    public async Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await _searchRepository.GetObservationsAsync(filter, cancellationToken);
    }

    public async Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20, CancellationToken cancellationToken = default)
    {
        var alltaxons = await _searchRepository.GetTaxonsAsync(name, maxCount, cancellationToken);
        return alltaxons;
    }

    public async Task<IEnumerable<AreaMarkerDto>> GetObservationsByZoomLevelAsync(int zoomLevel, CancellationToken cancellationToken = default)
    {
        //TODO, sjekk om vi kan fjerne denne og legge den til i GetObservationsAsync
        var areas = await _searchRepository.GetObservationsByZoomLevelAsync(zoomLevel, cancellationToken);
        return areas;
    }
}

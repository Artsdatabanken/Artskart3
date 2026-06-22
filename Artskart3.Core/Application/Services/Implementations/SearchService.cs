using Artskart3.Core.Application.Converters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Artskart3.Core.Application.Services.Implementations;

public class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepository;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan AreasCacheDuration = TimeSpan.FromHours(1);

    public SearchService(ISearchRepository searchRepository, IMemoryCache cache)
    {
        _searchRepository = searchRepository;
        _cache = cache;
    }

    public async Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null)
    {
        try
        {
            filter = filter ?? new LocationSearchFilterDto();

            var locations = _searchRepository.GetLocationsAsync(filter);
            return await GeoJsonConverter.LocationsToGeoJson(locations, StyleType.Unknown, filter.Epsg);
        }
        catch (ApplicationException ex)
        {
            throw new ApplicationException("An error occurred while processing your location search request.", ex);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while processing your location search request.", ex);
        }
    }

    public async Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = 20)
    {
        var alltaxons = await _searchRepository.GetTaxonsAsync(name, maxCount);
        return alltaxons;
    }

    public async Task<IEnumerable<AreaMarkerDto>> GetObservationsByZoomLevelAsync(int zoomLevel)
    {
        // Cacher resultater for zoomnivå 1 og 2 (fylker og kommuner)
        // da geometridata er store og sjelden endres
        if (zoomLevel is 1 or 2)
        {
            var cacheKey = $"areas_zoom_{zoomLevel}";
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<AreaMarkerDto>? cached))
            {
                cached = await _searchRepository.GetObservationsByZoomLevelAsync(zoomLevel);
                _cache.Set(cacheKey, cached, AreasCacheDuration);
            }
            return cached!;
        }

        return await _searchRepository.GetObservationsByZoomLevelAsync(zoomLevel);
    }
}

using Artskart3.Core.Application.Converters;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
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

    public async Task<string> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        filter = filter ?? new LocationSearchFilterDto();

        try
        {
            var locations = await _searchRepository.GetLocationsAsync(filter, cancellationToken);
            return GeoJsonConverter.LocationsToCompactJson(locations, filter.Epsg);
        }
        catch (ApplicationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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

    public async Task<IEnumerable<AreaMarkerDto>> GetAreaMarkersAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        var hasFilters = filter?.HasActiveFilters == true;

        // Cacher kun ufiltrerte resultater for zoomnivå 1 og 2 (fylker og kommuner)
        // da geometridata er store og sjelden endres
        if (!hasFilters && zoomLevel is 1 or 2)
        {
            var cacheKey = $"areas_zoom_{zoomLevel}";
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<AreaMarkerDto>? cached))
            {
                cached = await _searchRepository.GetAreaMarkersAsync(zoomLevel, filter, cancellationToken);
                _cache.Set(cacheKey, cached, AreasCacheDuration);
            }
            return cached!;
        }

        return await _searchRepository.GetAreaMarkersAsync(zoomLevel, filter, cancellationToken);
    }
}

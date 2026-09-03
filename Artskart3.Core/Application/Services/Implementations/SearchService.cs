using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly TimeSpan AreaCountsCacheDuration = TimeSpan.FromMinutes(5);

    public SearchService(ISearchRepository searchRepository, IMemoryCache cache)
    {
        _searchRepository = searchRepository;
        _cache = cache;
    }

    public async Task<List<LocationModel>> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new LocationSearchFilterDto();
        return await _searchRepository.GetLocationsAsync(filter, cancellationToken);
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

    public async Task<IEnumerable<LocationPolygonDto>> GetLocationPolygonsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        return await _searchRepository.GetLocationPolygonsAsync(filter, cancellationToken);
    }

    public async Task<AreaCountsResultDto> GetAreaCountsAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        // Bygg cache-nøkkel fra filter + zoomnivå
        var filterJson = JsonSerializer.Serialize(filter ?? new LocationSearchFilterDto());
        var keyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(filterJson)));
        var cacheKey = $"area_counts_{zoomLevel}_{keyHash}";

        if (_cache.TryGetValue(cacheKey, out AreaCountsResultDto? cached))
        {
            return cached!;
        }

        var hasFilters = filter?.HasActiveFilters == true;
        AreaCountDto[] countsArray;

        if (!hasFilters && zoomLevel is 1 or 2)
        {
            var markers = await GetAreaMarkersAsync(zoomLevel, null, cancellationToken);
            countsArray = markers.Select(m => new AreaCountDto
            {
                Fid = m.Fid,
                ObservationCount = m.ObservationCount ?? 0
            }).ToArray();
        }
        else
        {
            countsArray = (await _searchRepository.GetAreaCountsAsync(zoomLevel, filter, cancellationToken)).ToArray();
        }

        var json = JsonSerializer.Serialize(countsArray);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(json));
        var etag = $"\"{Convert.ToHexStringLower(hash)}\"";

        var result = new AreaCountsResultDto(countsArray, etag);
        _cache.Set(cacheKey, result, AreaCountsCacheDuration);

        return result;
    }
}

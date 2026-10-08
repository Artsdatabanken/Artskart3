using System.IO.Compression;
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

    /// <summary>
    /// Låser for å unngå at en kald buffer bygges mange ganger samtidig.
    ///
    /// Uten dem kjører N samtidige kall hele områdespørringen hver, serialiserer
    /// 20,7 MB hver og gzipper hver — hundrevis av millisekunder CPU, N ganger, for
    /// et resultat alle skal dele. Striper framfor én lås per nøkkel fordi
    /// områdetellingene nøkles på en filterhash: nøkkelrommet er ubegrenset, og et
    /// låseregister per nøkkel ville vokst med det. Samme mønster som
    /// ObservationMapService.
    ///
    /// Statisk fordi tjenesten er Scoped: én lås per forespørsel ville ikke låst noe.
    /// </summary>
    private static readonly SemaphoreSlim[] CacheLocks =
        Enumerable.Range(0, 16).Select(_ => new SemaphoreSlim(1)).ToArray();

    private static SemaphoreSlim LockFor(string cacheKey) =>
        CacheLocks[(uint)cacheKey.GetHashCode(StringComparison.Ordinal) % CacheLocks.Length];

    /// <summary>
    /// Omtrentlig minnebruk for en bufret post, i byte. Den delte IMemoryCache har en
    /// SizeLimit, og da MÅ hver Set oppgi en størrelse — uten den kaster Set. Tallet
    /// trenger ikke være presist, bare proporsjonalt: det er det som gjør at de store
    /// geometripostene kan kastes ut under press framfor at bufferen vokser fritt.
    /// </summary>
    private static long EstimatedSize(IEnumerable<AreaMarkerDto> areas) =>
        areas.Sum(a => (long)((a.WktsPolygon?.Length ?? 0) * 2) + 256);

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

    public async Task<LocationCountDto> GetLocationCountAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new LocationSearchFilterDto();
        return await _searchRepository.GetLocationCountAsync(filter, cancellationToken);
    }

    public async Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default)
    {
        return await _searchRepository.GetObservationsAsync(filter, cancellationToken);
    }

    public async Task<IEnumerable<ObservationListInfoDto>> GetObservationListInfo(ObservationListInfoRequestDto request, CancellationToken cancellationToken = default)
    {
        IEnumerable<ObservationListInfoDto> observations = await _searchRepository.GetObservationListInfo(request, cancellationToken);
        return observations;
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
            if (_cache.TryGetValue(cacheKey, out IEnumerable<AreaMarkerDto>? cached))
                return cached!;

            var laas = LockFor(cacheKey);
            await laas.WaitAsync(cancellationToken);
            try
            {
                // Sjekkes på nytt: en annen forespørsel kan ha bygget den mens vi
                // ventet, og da skal vi ikke bygge den om igjen.
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached!;

                var bygget = await _searchRepository.GetAreaMarkersAsync(zoomLevel, filter, cancellationToken);
                _cache.Set(cacheKey, bygget, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = AreasCacheDuration,
                    Size = EstimatedSize(bygget),

                    // NeverRemove: MemoryCache kjenner ikke gjenoppbyggingskost og
                    // kaster ut etter alder blant poster med samme prioritet. Uten
                    // dette kunne denne posten — stor og dyr — blitt kastet for å gi
                    // plass til en 46 kB tellepost, og neste kall måtte bygge den på
                    // nytt. Det er maks to slike poster (zoomnivå 1 og 2), så
                    // mengden er kjent og begrenset. Tellepostene står på Normal med
                    // vilje: det er DE som skal vike når grensen nås.
                    Priority = CacheItemPriority.NeverRemove,
                });
                return bygget;
            }
            finally
            {
                laas.Release();
            }
        }

        return await _searchRepository.GetAreaMarkersAsync(zoomLevel, filter, cancellationToken);
    }

    /// <summary>
    /// JSON-innstillingene MÅ speile dem MVC bruker (Program.cs: AddControllers().AddJsonOptions).
    /// Serialiseringen skjer her framfor i rammeverket, så et avvik ville gitt klienten
    /// et annet format enn den får fra alle andre endepunkter — f.eks. PascalCase-nøkler.
    /// AreaMarkersPayloadMatcherTests vokter det.
    /// </summary>
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    /// <inheritdoc />
    public async Task<AreaMarkersPayloadDto> GetAreaMarkersPayloadAsync(int zoomLevel, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"areas_payload_zoom_{zoomLevel}";
        if (_cache.TryGetValue(cacheKey, out AreaMarkersPayloadDto? cached))
            return cached!;

        // Låses: dette er det dyreste byggearbeidet i tjenesten — 20,7 MB
        // serialisering pluss gzip på Optimal — og uten låsen gjør N samtidige kall
        // på en kald buffer det N ganger.
        var laas = LockFor(cacheKey);
        await laas.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(cacheKey, out cached))
                return cached!;

            // Går via GetAreaMarkersAsync slik at DTO-cachen deles med den vanlige stien —
            // to bufre over samme data ville kunne komme i utakt.
            var areas = (await GetAreaMarkersAsync(zoomLevel, null, cancellationToken)).ToArray();

            var raw = JsonSerializer.SerializeToUtf8Bytes(areas, PayloadJsonOptions);

            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(raw, 0, raw.Length);
            }

            var gzipped = buffer.ToArray();
            var payload = new AreaMarkersPayloadDto(raw, gzipped);
            _cache.Set(cacheKey, payload, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AreasCacheDuration,
                Size = raw.Length + gzipped.Length,

                // Den dyreste posten i bufferen — ~297 ms serialisering pluss gzip
                // på Optimal over 20,7 MB. Skjermes fra utkasting, se
                // GetAreaMarkersAsync. Maks to poster.
                Priority = CacheItemPriority.NeverRemove,
            });
            return payload;
        }
        finally
        {
            laas.Release();
        }
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
                ObservationCount = m.ObservationCount
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

        // Nøkkelen er en filterhash, så nøkkelrommet er ubegrenset — og det er
        // nettopp derfor størrelsen må oppgis: SizeLimit er det som gjør at mange
        // ulike filtre ikke kan fylle minnet. Én celle er to korte felt; 128 byte
        // er rikelig per celle og trenger ikke være presist.
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AreaCountsCacheDuration,
            Size = countsArray.Length * 128L + 256,
        });

        return result;
    }
}

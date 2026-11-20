using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Constants;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Slår opp matrikkelenheter i Kartverkets åpne Eiendom API og finner lokasjonene
/// som snitter dem, se <see cref="LocationIntersection"/>.
/// </summary>
public class CadastralUnitService : ICadastralUnitService
{
    // Kartverket oppdaterer fra Matrikkelen én gang i døgnet.
    private static readonly TimeSpan GeometryCacheDuration = TimeSpan.FromHours(24);

    // Feil huskes kort, så forespørsler som venter på samme eiendom feiler med en gang i
    // stedet for å vente på hver sin timeout etter tur.
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromSeconds(30);

    // Samtidige bom på samme eiendom venter på hverandre i stedet for å gjøre hvert sitt
    // oppslag. Statiske fordi tjenesten lages per forespørsel. To sett, så
    // lokasjonsoppslaget kan hente geometrien uten å vente på en lås det selv holder.
    private static readonly SemaphoreSlim[] GeometryLocks = LocationIntersection.CreateLocks();
    private static readonly SemaphoreSlim[] LocationIdsLocks = LocationIntersection.CreateLocks();

    // Et NTS-punkt er et Coordinate-objekt pluss en referanse.
    private const long BytesPerPoint = 48;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), SearchConstants.DefaultEpsgCode);

    private readonly HttpClient _httpClient;
    private readonly IArtsKartDbContext _context;
    private readonly IMemoryCache _cache;

    public CadastralUnitService(HttpClient httpClient, IArtsKartDbContext context, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _context = context;
        _cache = cache;
    }

    public async Task<CadastralUnitDto?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var normalizedId = CadastralUnitId.Normalize(id)
            ?? throw new ArgumentException($"'{id}' is not a valid cadastral unit number.", nameof(id));

        var geometry = await GetGeometryAsync(normalizedId, cancellationToken);
        if (geometry == null) return null;

        var envelope = geometry.EnvelopeInternal;
        return new CadastralUnitDto
        {
            Id = normalizedId,
            WktPolygon = geometry.AsText(),
            Extent = new MapExtentDto { MinX = envelope.MinX, MinY = envelope.MinY, MaxX = envelope.MaxX, MaxY = envelope.MaxY },
        };
    }

    public async Task<int[]> GetLocationIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids.Select(CadastralUnitId.Normalize).OfType<string>().Distinct().ToArray();

        // Kartverket-kallene kan gå samtidig, databasespørringene ikke (samme DbContext).
        await Task.WhenAll(normalizedIds.Select(id => GetGeometryAsync(id, cancellationToken)));

        var locationIds = new HashSet<int>();
        foreach (var id in normalizedIds)
        {
            locationIds.UnionWith(await GetLocationIdsAsync(id, cancellationToken));
        }
        return [.. locationIds];
    }

    private Task<int[]> GetLocationIdsAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateOnceAsync($"cadastral_unit_locations_{id}", LocationIdsLocks, async entry =>
        {
            var ids = await FindLocationIdsAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = ids == null
                ? LocationIntersection.EmptyResultCacheDuration
                : LocationIntersection.LocationIdsCacheDuration;
            entry.Size = LocationIntersection.EntryOverheadBytes + (ids?.Length ?? 0) * sizeof(int);
            return ids ?? [];
        }, cancellationToken);

    /// <summary>
    /// Null når eiendommen ikke finnes eller ingen lokasjon har geometri.
    /// </summary>
    private async Task<int[]?> FindLocationIdsAsync(string id, CancellationToken cancellationToken)
    {
        var geometry = await GetGeometryAsync(id, cancellationToken);
        if (geometry == null) return null;

        // Teigene kan være ugyldige hver for seg (selvkrysning) og sammen (overlapp).
        // Oppslaget reparerer dem, siden SQL Server avviser dem.
        return await LocationIntersection.FindLocationIdsAsync(_context, _cache, geometry, cancellationToken);
    }

    private Task<MultiPolygon?> GetGeometryAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateOnceAsync($"cadastral_unit_geometry_{id}", GeometryLocks, async entry =>
        {
            var geometry = await FetchRememberingFailureAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = geometry == null ? LocationIntersection.EmptyResultCacheDuration : GeometryCacheDuration;
            entry.Size = LocationIntersection.EntryOverheadBytes + (geometry?.NumPoints ?? 0) * BytesPerPoint;
            return geometry;
        }, cancellationToken);

    private async Task<MultiPolygon?> FetchRememberingFailureAsync(string id, CancellationToken cancellationToken)
    {
        var failureKey = $"cadastral_unit_failure_{id}";
        if (_cache.TryGetValue(failureKey, out _))
        {
            throw new HttpRequestException($"Kartverket failed for {id} less than {FailureCacheDuration.TotalSeconds} seconds ago.");
        }

        try
        {
            return await FetchGeometryAsync(id, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _cache.Set(failureKey, true, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = FailureCacheDuration, Size = LocationIntersection.EntryOverheadBytes });
            throw;
        }
    }

    private async Task<MultiPolygon?> FetchGeometryAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"geokoding?matrikkelnummer={Uri.EscapeDataString(id)}&omrade=true&utkoordsys={SearchConstants.DefaultEpsgCode}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var collection = await response.Content.ReadFromJsonAsync<KartverketFeatureCollection>(JsonOptions, cancellationToken);
        var polygons = collection?.Features
            .Select(f => f.Geometry)
            .OfType<KartverketGeometry>()
            .SelectMany(ToPolygons)
            .ToArray() ?? [];

        return polygons.Length == 0 ? null : Factory.CreateMultiPolygon(polygons);
    }

    private static IEnumerable<Polygon> ToPolygons(KartverketGeometry geometry) => geometry.Type switch
    {
        "Polygon" => [ToPolygon(geometry.Coordinates)],
        "MultiPolygon" => geometry.Coordinates.EnumerateArray().Select(ToPolygon),
        _ => [],
    };

    private static Polygon ToPolygon(JsonElement rings)
    {
        var linearRings = rings.EnumerateArray().Select(ToLinearRing).ToArray();
        return Factory.CreatePolygon(linearRings[0], linearRings[1..]);
    }

    private static LinearRing ToLinearRing(JsonElement ring) =>
        Factory.CreateLinearRing(ring.EnumerateArray()
            .Select(point => new Coordinate(point[0].GetDouble(), point[1].GetDouble()))
            .ToArray());
}

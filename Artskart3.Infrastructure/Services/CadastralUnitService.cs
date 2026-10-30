using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Slår opp matrikkelenheter i Kartverkets åpne Eiendom API og finner lokasjonene
/// som snitter dem.
///
/// Snittet tas med STIntersects mot Location.Geometry, samme regel som LocationAreas
/// er bygget med (se Scripts/RebuildLocationAreasForAreaTypes.sql). En lokasjon med
/// flate- eller linjegeometri treffer derfor selv om punktet (East/North) ligger
/// utenfor eiendommen.
/// </summary>
public class CadastralUnitService : ICadastralUnitService
{
    private const int Utm33Srid = 25833;

    // Kartverket oppdaterer fra Matrikkelen én gang i døgnet.
    private static readonly TimeSpan GeometryCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan LocationIdsCacheDuration = TimeSpan.FromHours(1);

    // Tomme svar bufres kort, så en eiendom eller lokasjonsgeometri som kommer til
    // ikke står som manglende et helt døgn.
    private static readonly TimeSpan EmptyResultCacheDuration = TimeSpan.FromMinutes(10);

    // Samtidige bom på samme eiendom venter på hverandre i stedet for å gjøre hvert sitt
    // oppslag. Statiske fordi tjenesten lages per forespørsel. To sett, så
    // lokasjonsoppslaget kan hente geometrien uten å vente på en lås det selv holder.
    private static readonly SemaphoreSlim[] GeometryLocks = CreateLocks();
    private static readonly SemaphoreSlim[] LocationIdsLocks = CreateLocks();

    // Omtrentlig størrelse i byte. Den delte bufferen i API-et har SizeLimit, og da må
    // hver post oppgi Size. Et NTS-punkt er et Coordinate-objekt pluss en referanse.
    private const long EntryOverheadBytes = 256;
    private const long BytesPerPoint = 48;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), Utm33Srid);

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
        GetOrCreateOnceAsync($"cadastral_unit_locations_{id}", LocationIdsLocks, async entry =>
        {
            var ids = await FindLocationIdsAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = ids == null ? EmptyResultCacheDuration : LocationIdsCacheDuration;
            entry.Size = EntryOverheadBytes + (ids?.Length ?? 0) * sizeof(int);
            return ids ?? [];
        }, cancellationToken);

    /// <summary>
    /// Null når eiendommen ikke finnes eller ingen lokasjon har geometri.
    /// </summary>
    private async Task<int[]?> FindLocationIdsAsync(string id, CancellationToken cancellationToken)
    {
        var geometry = await GetGeometryAsync(id, cancellationToken);
        if (geometry == null) return null;

        var srid = await GetLocationSridAsync(cancellationToken);
        if (srid == null) return null;

        // Teigene slås sammen først: overlappende teiger er en ugyldig MultiPolygon, og
        // SQL Server avviser ugyldige geometrier.
        var property = NtsGeometryServices.Instance.CreateGeometryFactory(srid.Value).CreateGeometry(geometry.Union());

        return await _context.Set<Location>()
            .AsNoTracking()
            .Where(l => l.Geometry != null && l.Geometry.Intersects(property))
            .Select(l => l.Id)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// SRID-en til Location.Geometry, 32633 i produksjon. Med ulike SRID-er gir
    /// STIntersects NULL, altså ingen treff og ingen feil, så eiendommen får den samme.
    /// Null når ingen lokasjon har geometri.
    /// </summary>
    private Task<int?> GetLocationSridAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync("location_geometry_srid", async entry =>
        {
            var srid = await _context.Set<Location>()
                .Where(l => l.Geometry != null)
                .Select(l => (int?)l.Geometry!.SRID)
                .FirstOrDefaultAsync(cancellationToken);

            entry.AbsoluteExpirationRelativeToNow = srid == null ? EmptyResultCacheDuration : GeometryCacheDuration;
            entry.Size = EntryOverheadBytes;
            return srid;
        });

    private Task<MultiPolygon?> GetGeometryAsync(string id, CancellationToken cancellationToken) =>
        GetOrCreateOnceAsync($"cadastral_unit_geometry_{id}", GeometryLocks, async entry =>
        {
            var geometry = await FetchGeometryAsync(id, cancellationToken);
            entry.AbsoluteExpirationRelativeToNow = geometry == null ? EmptyResultCacheDuration : GeometryCacheDuration;
            entry.Size = EntryOverheadBytes + (geometry?.NumPoints ?? 0) * BytesPerPoint;
            return geometry;
        }, cancellationToken);

    /// <summary>
    /// Som GetOrCreateAsync, men samtidige bom på samme nøkkel kjører fabrikken bare én gang.
    /// De andre venter og leser svaret fra bufferen.
    /// </summary>
    private async Task<T> GetOrCreateOnceAsync<T>(
        string key, SemaphoreSlim[] locks, Func<ICacheEntry, Task<T>> create, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out T? cached)) return cached!;

        var gate = locks[Math.Abs(StringComparer.Ordinal.GetHashCode(key) % locks.Length)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await _cache.GetOrCreateAsync(key, create))!;
        }
        finally
        {
            gate.Release();
        }
    }

    private static SemaphoreSlim[] CreateLocks() => Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();

    private async Task<MultiPolygon?> FetchGeometryAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"geokoding?matrikkelnummer={Uri.EscapeDataString(id)}&omrade=true&utkoordsys={Utm33Srid}";

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

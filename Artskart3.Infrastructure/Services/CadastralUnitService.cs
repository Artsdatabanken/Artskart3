using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Slår opp matrikkelenheter i Kartverkets åpne Eiendom API og finner lokasjonene
/// innenfor dem.
///
/// Punkt-i-polygon gjøres i minnet på Location.East/North (UTM33), etter et
/// envelope-søk per teig som treffer IX_EastNorth. Location.Geometry kan ikke brukes:
/// den skrives ikke av importen, og SRID-en er ukjent.
/// </summary>
public class CadastralUnitService : ICadastralUnitService
{
    private const int Utm33Srid = 25833;

    // Kartverket oppdaterer fra Matrikkelen én gang i døgnet.
    private static readonly TimeSpan GeometryCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan LocationIdsCacheDuration = TimeSpan.FromHours(1);

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

    private async Task<int[]> GetLocationIdsAsync(string id, CancellationToken cancellationToken)
    {
        var locationIds = await _cache.GetOrCreateAsync($"cadastral_unit_locations_{id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LocationIdsCacheDuration;

            var geometry = await GetGeometryAsync(id, cancellationToken);
            if (geometry == null) return [];

            // Hver teig for seg: teigene kan ligge langt fra hverandre, og én envelope rundt
            // alle ville hentet alt som ligger mellom dem.
            var ids = new HashSet<int>();
            for (var i = 0; i < geometry.NumGeometries; i++)
            {
                ids.UnionWith(await GetLocationIdsInParcelAsync(geometry.GetGeometryN(i), cancellationToken));
            }
            return ids.ToArray();
        });

        return locationIds ?? [];
    }

    private async Task<IEnumerable<int>> GetLocationIdsInParcelAsync(Geometry parcel, CancellationToken cancellationToken)
    {
        var envelope = parcel.EnvelopeInternal;
        var minX = (int)Math.Floor(envelope.MinX);
        var maxX = (int)Math.Ceiling(envelope.MaxX);
        var minY = (int)Math.Floor(envelope.MinY);
        var maxY = (int)Math.Ceiling(envelope.MaxY);

        var candidates = await _context.Set<Location>()
            .AsNoTracking()
            .Where(l => l.East >= minX && l.East <= maxX && l.North >= minY && l.North <= maxY)
            .Select(l => new { l.Id, l.East, l.North })
            .ToListAsync(cancellationToken);

        var prepared = PreparedGeometryFactory.Prepare(parcel);
        return candidates
            .Where(l => prepared.Covers(Factory.CreatePoint(new Coordinate(l.East, l.North))))
            .Select(l => l.Id);
    }

    private Task<MultiPolygon?> GetGeometryAsync(string id, CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync($"cadastral_unit_geometry_{id}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = GeometryCacheDuration;
            return FetchGeometryAsync(id, cancellationToken);
        });

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

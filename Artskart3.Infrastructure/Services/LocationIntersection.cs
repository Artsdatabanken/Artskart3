using Artskart3.Core.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Lokasjonene som snitter en flate. Felles for matrikkelenheter og egendefinerte
/// områder, så begge bruker samme regel.
///
/// Snittet tas med STIntersects mot Location.Geometry, samme regel som LocationAreas
/// er bygget med (se Scripts/RebuildLocationAreasForAreaTypes.sql). En lokasjon med
/// flate- eller linjegeometri treffer derfor selv om punktet (East/North) ligger
/// utenfor flaten.
/// </summary>
internal static class LocationIntersection
{
    public static readonly TimeSpan LocationIdsCacheDuration = TimeSpan.FromHours(1);

    // Tomme svar bufres kort, så det som kommer til ikke står som manglende lenge.
    public static readonly TimeSpan EmptyResultCacheDuration = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan SridCacheDuration = TimeSpan.FromHours(24);

    // Omtrentlig størrelse i byte. Den delte bufferen i API-et har SizeLimit, og da må
    // hver post oppgi Size.
    public const long EntryOverheadBytes = 256;

    /// <summary>
    /// Null når ingen lokasjon har geometri. Med <paramref name="maxCount"/> stopper
    /// oppslaget etter så mange lokasjoner.
    /// </summary>
    public static async Task<int[]?> FindLocationIdsAsync(
        IArtsKartDbContext context, IMemoryCache cache, Geometry area, CancellationToken cancellationToken, int? maxCount = null)
    {
        var srid = await GetLocationSridAsync(context, cache, cancellationToken);
        if (srid == null) return null;

        // SQL Server avviser ugyldige geometrier, for eksempel selvkryssende polygoner.
        var validArea = NtsGeometryServices.Instance.CreateGeometryFactory(srid.Value).CreateGeometry(GeometryFixer.Fix(area));
        if (validArea.IsEmpty) return [];

        var ids = context.Set<Location>()
            .AsNoTracking()
            .Where(l => l.Geometry != null && l.Geometry.Intersects(validArea))
            .Select(l => l.Id);

        return await (maxCount is { } max ? ids.Take(max) : ids).ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Flaten med samme SRID som Location.Geometry og Area.WktPolygon, reparert så SQL Server
    /// godtar den. Null når ingen lokasjon har geometri.
    /// </summary>
    public static async Task<Geometry?> PrepareAreaAsync(
        IArtsKartDbContext context, IMemoryCache cache, Geometry area, CancellationToken cancellationToken)
    {
        var srid = await GetLocationSridAsync(context, cache, cancellationToken);
        return srid == null
            ? null
            : NtsGeometryServices.Instance.CreateGeometryFactory(srid.Value).CreateGeometry(GeometryFixer.Fix(area));
    }

    /// <summary>
    /// SRID-en til Location.Geometry, 32633 i produksjon. Med ulike SRID-er gir
    /// STIntersects NULL, altså ingen treff og ingen feil, så flaten får den samme.
    /// Null når ingen lokasjon har geometri.
    /// </summary>
    private static Task<int?> GetLocationSridAsync(IArtsKartDbContext context, IMemoryCache cache, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync("location_geometry_srid", async entry =>
        {
            var srid = await context.Set<Location>()
                .Where(l => l.Geometry != null)
                .Select(l => (int?)l.Geometry!.SRID)
                .FirstOrDefaultAsync(cancellationToken);

            entry.AbsoluteExpirationRelativeToNow = srid == null ? EmptyResultCacheDuration : SridCacheDuration;
            entry.Size = EntryOverheadBytes;
            return srid;
        });

    /// <summary>
    /// Som GetOrCreateAsync, men samtidige bom på samme nøkkel kjører fabrikken bare én gang.
    /// De andre venter og leser svaret fra bufferen.
    /// </summary>
    public static async Task<T> GetOrCreateOnceAsync<T>(
        this IMemoryCache cache, string key, SemaphoreSlim[] locks, Func<ICacheEntry, Task<T>> create, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out T? cached)) return cached!;

        var gate = locks[Math.Abs(StringComparer.Ordinal.GetHashCode(key) % locks.Length)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await cache.GetOrCreateAsync(key, create))!;
        }
        finally
        {
            gate.Release();
        }
    }

    public static SemaphoreSlim[] CreateLocks() => Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();
}

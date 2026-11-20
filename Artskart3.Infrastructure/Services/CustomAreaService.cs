using System.Security.Cryptography;
using System.Text;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Constants;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using AreaType = Artskart3.Core.Domain.Enums.AreaType;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Finner lokasjonene i et egendefinert område, se <see cref="LocationIntersection"/>.
///
/// Ett kartbilde sender flere forespørsler med samme polygon (lokasjoner, antall,
/// polygoner, områdeantall), så lokasjonene bufres på polygonet og samtidige
/// forespørsler deler ett oppslag.
///
/// Begge oppslagene har tak. Lokasjonsoppslaget stopper like over grensen, og
/// oppdelingen leser høyst <see cref="MaxEdgeCandidates"/> kandidater og stopper når
/// kanten er over grensen. Et for stort område koster derfor ikke mer enn et som så vidt
/// er innenfor, og listen bufres ikke.
/// </summary>
public class CustomAreaService(
    IArtsKartDbContext context, IMemoryCache cache, ILogger<CustomAreaService> logger,
    int maxLocations = SearchConstants.MaxCustomAreaLocations,
    int maxEdgeCandidates = CustomAreaService.MaxEdgeCandidates) : ICustomAreaService
{
    /// <summary>
    /// Kandidatene oppdelingen leser, fra områdene polygonet krysser. Målt: 467 000 rundt Oslo og
    /// 960 000 for et kryss over grensen. Over taket deles ikke polygonet, og da må hele
    /// lokasjonslisten holde seg under grensen. Uten tak kunne et tynt, taggete polygon over store
    /// områder hentet millioner av rader.
    /// </summary>
    public const int MaxEdgeCandidates = 2_000_000;

    private static readonly SemaphoreSlim[] Locks = LocationIntersection.CreateLocks();
    private static readonly SemaphoreSlim[] SplitLocks = LocationIntersection.CreateLocks();

    private readonly LocationListStore _lists = new(context, logger);

    // Områdetypene som dekker landet og havet uten å overlappe hverandre innenfor typen.
    private static readonly int[] SplitAreaTypes =
    [
        (int)AreaType.Municipality,
        (int)AreaType.OceanArea,
        (int)AreaType.SvalbardBjørnøyaAndJanMayen,
    ];

    // Et fylke helt innenfor erstatter kommunene sine, så listevisningen får færre grener.
    private static readonly int[] InsideAreaTypes = [.. SplitAreaTypes, (int)AreaType.County];

    public Task<int[]?> GetLocationIdsAsync(string wkt, CancellationToken cancellationToken = default)
    {
        var polygon = CustomArea.Parse(wkt)
            ?? throw new ArgumentException("Not a valid custom area polygon.", nameof(wkt));

        var key = $"custom_area_locations_{Hash(wkt)}";

        return cache.GetOrCreateOnceAsync(key, Locks, async entry =>
        {
            var ids = await LocationIntersection.FindLocationIdsAsync(
                context, cache, polygon, cancellationToken, maxCount: maxLocations + 1) ?? [];
            var tooMany = ids.Length > maxLocations;
            entry.AbsoluteExpirationRelativeToNow = ids.Length == 0
                ? LocationIntersection.EmptyResultCacheDuration
                : LocationIntersection.LocationIdsCacheDuration;
            entry.Size = LocationIntersection.EntryOverheadBytes + (tooMany ? 0 : ids.Length * sizeof(int));
            if (tooMany) return null;

            _lists.SaveInBackground(ids);
            return ids;
        }, cancellationToken);
    }

    public Task<CustomAreaSplit?> GetSplitAsync(string wkt, CancellationToken cancellationToken = default)
    {
        var polygon = CustomArea.Parse(wkt)
            ?? throw new ArgumentException("Not a valid custom area polygon.", nameof(wkt));

        return cache.GetOrCreateOnceAsync($"custom_area_split_{Hash(wkt)}", SplitLocks, async entry =>
        {
            var split = await FindSplitAsync(polygon, cancellationToken);
            _lists.SaveInBackground(split?.EdgeLocationIds);
            entry.AbsoluteExpirationRelativeToNow = LocationIntersection.LocationIdsCacheDuration;
            entry.Size = LocationIntersection.EntryOverheadBytes
                         + (split?.InsideAreas.Count ?? 0) * 64
                         + (split?.EdgeLocationIds?.Length ?? 0) * sizeof(int);
            return split;
        }, cancellationToken);
    }

    private async Task<CustomAreaSplit?> FindSplitAsync(NetTopologySuite.Geometries.Polygon polygon, CancellationToken cancellationToken)
    {
        var area = await LocationIntersection.PrepareAreaAsync(context, cache, polygon, cancellationToken);
        if (area is null || area.IsEmpty) return null;

        var types = InsideAreaTypes;
        var touched = await context.Set<Area>()
            .AsNoTracking()
            .Where(a => a.IsCurrent && types.Contains(a.AreaTypeId) && a.WktPolygon != null && a.WktPolygon.Intersects(area))
            .Select(a => new { a.Id, a.AreaTypeId, a.Fid, a.ParentFid, Inside = a.WktPolygon!.Within(area) })
            .ToListAsync(cancellationToken);

        var insideCounties = touched.Where(a => a.Inside && a.AreaTypeId == (int)AreaType.County).Select(a => a.Fid).ToHashSet();
        var inside = touched
            .Where(a => a.Inside && !(a.AreaTypeId == (int)AreaType.Municipality && insideCounties.Contains(a.ParentFid)))
            .ToList();
        if (inside.Count == 0) return null;

        // Kanten er lokasjonene i polygonet uten rad i noe område helt innenfor. Et punkt i
        // polygonet ligger enten i et område helt innenfor, i et område som krysser kanten,
        // eller utenfor alle områder. Kandidatene hentes derfor fra områdene som krysser
        // kanten, og fra de få lokasjonene uten områderader, i stedet for fra hele polygonet.
        var insideIds = inside.Select(a => a.Id).ToArray();
        var straddlingIds = touched
            .Where(a => !a.Inside && a.AreaTypeId != (int)AreaType.County)
            .Select(a => a.Id)
            .ToArray();

        // Kandidatene hentes med koordinatene, og punktene, nesten alle, testes i minnet. Med
        // STIntersects i SQL var det tregeste steget: målt 3,2 s for 467 000 kandidater rundt
        // Oslo, mot 1,9 s for å hente dem. Punkter utenfor polygonets boks hentes ikke.
        //
        // Rå SQL fordi EF la områdelistene ut som enkeltparametere og slo opp Area på nytt i
        // NOT EXISTS. Den planen brukte 6-8 s på det samme.
        //
        // Områdene står som tall i teksten, ikke som OPENJSON. Med OPENJSON anslo SQL Server noen
        // titalls områder og slo opp hver lokasjon for seg: målt 2,0 mot 0,19 s for 40 x 32 km
        // rundt Oslo, og 5,0 mot 1,0 s for et kryss med 960 000 kandidater. ID-ene er heltall fra
        // Area, så de kan ikke føre inn SQL.
        var box = area.EnvelopeInternal;
        var sql = $"""
            SELECT DISTINCT TOP (@maxCandidates) l.Id, l.East, l.North, l.GeometryTypeId
            FROM dbo.LocationAreas la
            JOIN dbo.Location l ON l.Id = la.LocationId
            WHERE la.AreaId IN ({string.Join(",", straddlingIds)})
              AND (l.GeometryTypeId <> @point
                   OR (l.East BETWEEN @minX AND @maxX AND l.North BETWEEN @minY AND @maxY))
              AND NOT EXISTS (SELECT 1 FROM dbo.LocationAreas x
                              WHERE x.LocationId = l.Id
                                AND x.AreaId IN ({string.Join(",", insideIds)}))
            """;
        var insideAreas = inside.Select(a => (a.AreaTypeId, a.Fid)).ToList();
        var prepared = PreparedGeometryFactory.Prepare(area);
        var edgeIds = new List<int>();
        var shapes = new List<int>();
        if (straddlingIds.Length > 0)
        {
            var database = ((DbContext)context).Database;
            await database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = database.GetCommandTimeout() ?? command.CommandTimeout;
                command.Parameters.Add(new SqlParameter("@maxCandidates", maxEdgeCandidates + 1));
                command.Parameters.Add(new SqlParameter("@point", (byte)LocationGeometryType.Point));
                command.Parameters.Add(new SqlParameter("@minX", (int)Math.Floor(box.MinX)));
                command.Parameters.Add(new SqlParameter("@maxX", (int)Math.Ceiling(box.MaxX)));
                command.Parameters.Add(new SqlParameter("@minY", (int)Math.Floor(box.MinY)));
                command.Parameters.Add(new SqlParameter("@maxY", (int)Math.Ceiling(box.MaxY)));

                // Strømmes, så minnet ikke holder alle kandidatene. Ved et tak avbrytes kommandoen før
                // leseren lukkes, ellers ville databasen lest resten av radene opp til taket.
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var read = 0;
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (++read > maxEdgeCandidates)
                    {
                        command.Cancel();
                        return null;
                    }

                    var id = reader.GetInt32(0);
                    if (reader.GetByte(3) != (byte)LocationGeometryType.Point)
                    {
                        shapes.Add(id);
                        continue;
                    }
                    if (!prepared.Intersects(area.Factory.CreatePoint(new Coordinate(reader.GetInt32(1), reader.GetInt32(2))))) continue;

                    edgeIds.Add(id);
                    if (edgeIds.Count > maxLocations)
                    {
                        command.Cancel();
                        return new CustomAreaSplit(insideAreas, null);
                    }
                }
            }
            finally
            {
                await database.CloseConnectionAsync();
            }
        }

        // Flater og linjer testes mot geometrien; de er rundt én prosent av kandidatene.
        if (shapes.Count > 0)
        {
            var shapeIds = shapes.ToArray();
            edgeIds.AddRange(await context.Set<Location>()
                .AsNoTracking()
                .Where(l => EF.Parameter(shapeIds).Contains(l.Id) && l.Geometry!.Intersects(area))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken));
        }

        var withoutAreas = (await GetAllLocationsWithoutAreasAsync(cancellationToken))
            .Where(l => prepared.Intersects(l.Geometry))
            .Select(l => l.Id);
        int[] edge = [.. edgeIds.Union(withoutAreas)];

        return new CustomAreaSplit(insideAreas, edge.Length > maxLocations ? null : edge);
    }

    private const string LocationsWithoutAreasKey = "custom_area_locations_without_areas";
    private static readonly TimeSpan LocationsWithoutAreasCacheDuration = TimeSpan.FromHours(24);

    public async Task<int[]> GetLocationsWithoutAreasAsync(int[] locationIds, CancellationToken cancellationToken = default)
    {
        var withoutAreas = (await GetAllLocationsWithoutAreasAsync(cancellationToken)).Select(l => l.Id).ToHashSet();
        return [.. locationIds.Where(withoutAreas.Contains)];
    }

    // Hentes ved oppstart og fornyes før de går ut, se LocationsWithoutAreasWarmup. Dette er
    // reserven til de er hentet første gang.
    private Task<List<(int Id, Geometry Geometry)>> GetAllLocationsWithoutAreasAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateOnceAsync(LocationsWithoutAreasKey, Locks, async entry =>
        {
            var rows = await LoadLocationsWithoutAreasAsync(context, cancellationToken);
            ConfigureLocationsWithoutAreasEntry(entry, rows);
            return rows;
        }, cancellationToken);

    /// <summary>
    /// Lokasjonene uten rad i noen kommune, noe havområde eller Svalbard: rundt 200, de fleste
    /// på (0,0), resten like utenfor områdepolygonene. De kan ligge i et polygon uten å nås
    /// gjennom noe område. Bare gjeldende områder teller, som i indekstabellen. Spørringen leser
    /// alle lokasjonene og er målt til 3-12 s.
    /// </summary>
    public static async Task<List<(int Id, Geometry Geometry)>> LoadLocationsWithoutAreasAsync(
        IArtsKartDbContext context, CancellationToken cancellationToken)
    {
        var types = SplitAreaTypes;
        var rows = await context.Set<Location>()
            .AsNoTracking()
            .Where(l => l.Geometry != null && !l.Areas.Any(a => a.IsCurrent && types.Contains(a.AreaTypeId)))
            .Select(l => new { l.Id, l.Geometry })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Geometry!)).ToList();
    }

    public static void CacheLocationsWithoutAreas(IMemoryCache cache, List<(int Id, Geometry Geometry)> rows)
    {
        using var entry = cache.CreateEntry(LocationsWithoutAreasKey);
        entry.Value = rows;
        ConfigureLocationsWithoutAreasEntry(entry, rows);
    }

    private static void ConfigureLocationsWithoutAreasEntry(ICacheEntry entry, List<(int Id, Geometry Geometry)> rows)
    {
        entry.AbsoluteExpirationRelativeToNow = LocationsWithoutAreasCacheDuration;
        entry.Size = LocationIntersection.EntryOverheadBytes + rows.Sum(r => (long)r.Geometry.NumPoints * 48);
    }

    // Hash i stedet for teksten, som kan være lang.
    private static string Hash(string wkt) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(wkt)));
}

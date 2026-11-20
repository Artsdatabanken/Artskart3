using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.IO;
using AreaType = Artskart3.Core.Domain.Enums.AreaType;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Snittet mot Location.Geometry kjøres i SQL Server. Enhetstestene går mot InMemory,
/// som verken oversetter STIntersects eller avviser ugyldige geometrier.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class CustomAreaServiceIntegrationTests : IAsyncLifetime
{
    // Samme SRID som de andre testene bruker på Location.Geometry. Tabellen skal ha én.
    private const int Srid = 25833;
    private const string LookupPrefix = "custom-area-";

    private const string Square = "POLYGON ((910000 7910000, 910100 7910000, 910100 7910100, 910000 7910100, 910000 7910000))";

    // Sløyfe som krysser seg selv i (910 050, 7 910 050), slik en bruker kan tegne den.
    private const string Bowtie = "POLYGON ((910000 7910000, 910100 7910100, 910100 7910000, 910000 7910100, 910000 7910000))";

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    public CustomAreaServiceIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await _context.Set<Location>().Where(l => l.LookupId!.StartsWith(LookupPrefix)).ExecuteDeleteAsync();
        await _context.Set<Area>().Where(a => a.Fid.StartsWith(LookupPrefix)).ExecuteDeleteAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetLocationIdsAsync_FindsLocationsWhoseGeometryIntersectsTheArea()
    {
        var inside = await AddLocationAsync("inside", "POINT (910050 7910050)");
        await AddLocationAsync("outside", "POINT (910400 7910050)");

        // Første punkt, og dermed East/North, ligger utenfor området for disse to.
        var areaReachingIn = await AddLocationAsync(
            "area", "POLYGON ((910300 7910300, 910090 7910300, 910090 7910090, 910300 7910090, 910300 7910300))");
        var lineCrossing = await AddLocationAsync("line", "LINESTRING (909900 7910050, 910400 7910050)");

        var result = await CreateSut().GetLocationIdsAsync(Square);

        result.Should().BeEquivalentTo([inside.Id, areaReachingIn.Id, lineCrossing.Id]);
    }

    [Fact]
    public async Task GetLocationIdsAsync_WithSelfIntersectingArea_FindsLocationsInBothLoops()
    {
        var leftLoop = await AddLocationAsync("left", "POINT (910020 7910050)");
        var rightLoop = await AddLocationAsync("right", "POINT (910080 7910050)");
        await AddLocationAsync("between", "POINT (910050 7910080)");

        var result = await CreateSut().GetLocationIdsAsync(Bowtie);

        result.Should().BeEquivalentTo([leftLoop.Id, rightLoop.Id]);
    }

    [Fact]
    public async Task GetLocationIdsAsync_WithMoreLocationsThanTheLimit_ReturnsNull()
    {
        var first = await AddLocationAsync("first", "POINT (910010 7910010)");
        var second = await AddLocationAsync("second", "POINT (910020 7910020)");

        (await CreateSut(maxLocations: 2).GetLocationIdsAsync(Square)).Should().BeEquivalentTo([first.Id, second.Id]);

        await AddLocationAsync("third", "POINT (910030 7910030)");

        (await CreateSut(maxLocations: 2).GetLocationIdsAsync(Square)).Should().BeNull();
    }

    [Fact]
    public async Task GetLocationsWithoutAreasAsync_ReturnsTheListedLocationsWithoutAnyArea()
    {
        var area = await AddAreaAsync("area", "POLYGON ((910010 7910010, 910060 7910010, 910060 7910060, 910010 7910060, 910010 7910010))");
        var inArea = await AddLocationAsync("in-area", "POINT (910030 7910030)", area);
        var noArea = await AddLocationAsync("no-area", "POINT (910090 7910090)");
        await AddLocationAsync("no-area-not-listed", "POINT (910095 7910095)");

        (await CreateSut().GetLocationsWithoutAreasAsync([inArea.Id, noArea.Id])).Should().Equal(noArea.Id);
    }

    /// <summary>
    /// Indekstabellen har bare gjeldende områder, så en lokasjon som bare ligger i et nedlagt
    /// område, har ingen rader der.
    /// </summary>
    [Fact]
    public async Task GetLocationsWithoutAreasAsync_CountsOnlyCurrentAreas()
    {
        var retired = await AddAreaAsync("retired", "POLYGON ((910010 7910010, 910060 7910010, 910060 7910060, 910010 7910060, 910010 7910010))");
        await _context.Set<Area>().Where(a => a.Id == retired.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsCurrent, false));
        var inRetired = await AddLocationAsync("in-retired", "POINT (910030 7910030)", retired);

        (await CreateSut().GetLocationsWithoutAreasAsync([inRetired.Id])).Should().Equal(inRetired.Id);
    }

    /// <summary>
    /// Kommunen som ligger helt innenfor polygonet, brukes som område, og bare lokasjonene
    /// som ikke ligger i den, sendes som kant. Kommunen som stikker utenfor, brukes ikke.
    /// </summary>
    [Fact]
    public async Task GetSplitAsync_UsesAreasWhollyInsideAndListsOnlyTheEdge()
    {
        var inside = await AddAreaAsync("inside", "POLYGON ((910010 7910010, 910060 7910010, 910060 7910060, 910010 7910060, 910010 7910010))");
        var straddling = await AddAreaAsync("straddling", "POLYGON ((910070 7910010, 910200 7910010, 910200 7910060, 910070 7910060, 910070 7910010))");
        var inInside = await AddLocationAsync("in-inside", "POINT (910030 7910030)", inside);
        var inStraddling = await AddLocationAsync("in-straddling", "POINT (910080 7910030)", straddling);
        var noArea = await AddLocationAsync("no-area", "POINT (910090 7910090)");
        await AddLocationAsync("outside", "POINT (910150 7910030)", straddling);

        var split = await CreateSut().GetSplitAsync(Square);

        split.Should().NotBeNull();
        split!.InsideAreas.Should().Equal([((int)AreaType.Municipality, inside.Fid)]);
        split.EdgeLocationIds.Should().BeEquivalentTo([inStraddling.Id, noArea.Id]);
        inInside.Id.Should().NotBe(0);
    }

    /// <summary>
    /// Kandidatene er lokasjonene i områdene som krysser kanten, innenfor polygonets boks. Er de
    /// flere enn taket, deles ikke polygonet, og kanten avvises når den er over grensen, uten
    /// at resten leses.
    /// </summary>
    [Fact]
    public async Task GetSplitAsync_StopsAtTheCandidateAndEdgeLimits()
    {
        var inside = await AddAreaAsync("inside", "POLYGON ((910010 7910010, 910060 7910010, 910060 7910060, 910010 7910060, 910010 7910010))");
        var straddling = await AddAreaAsync("straddling", "POLYGON ((910070 7910010, 910200 7910010, 910200 7910060, 910070 7910060, 910070 7910010))");
        await AddLocationAsync("in-inside", "POINT (910030 7910030)", inside);
        await AddLocationAsync("edge-1", "POINT (910080 7910030)", straddling);
        await AddLocationAsync("edge-2", "POINT (910085 7910030)", straddling);
        await AddLocationAsync("outside", "POINT (910150 7910030)", straddling);

        (await CreateSut().GetSplitAsync(Square))!.EdgeLocationIds.Should().HaveCount(2);
        (await CreateSut(maxEdgeCandidates: 1).GetSplitAsync(Square)).Should().BeNull();

        var tooLarge = await CreateSut(maxLocations: 1).GetSplitAsync(Square);
        tooLarge.Should().NotBeNull();
        tooLarge!.EdgeLocationIds.Should().BeNull();
    }

    [Fact]
    public async Task GetSplitAsync_WithoutAreasWhollyInside_ReturnsNull()
    {
        var straddling = await AddAreaAsync("straddling", "POLYGON ((910070 7910010, 910200 7910010, 910200 7910060, 910070 7910060, 910070 7910010))");
        await AddLocationAsync("in-straddling", "POINT (910080 7910030)", straddling);

        (await CreateSut().GetSplitAsync(Square)).Should().BeNull();
    }

    private async Task<Area> AddAreaAsync(string name, string wkt)
    {
        var geometry = new WKTReader().Read(wkt);
        geometry.SRID = Srid;
        var area = new Area
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            Fid = LookupPrefix + name,
            Name = LookupPrefix + name,
            AreaTypeId = (int)AreaType.Municipality,
            ZoomLevel = 2,
            ParentFid = "custom-area-parent",
            SyncDateTime = DateTime.UtcNow,
            Bbox = "bbox",
            TimeStamp = DateTime.UtcNow,
            IsCurrent = true,
            WktPolygon = geometry,
        };
        _context.Set<Area>().Add(area);
        await _context.SaveChangesAsync();
        return area;
    }

    private CustomAreaService CreateSut(int maxLocations = 1000, int maxEdgeCandidates = CustomAreaService.MaxEdgeCandidates) =>
        new(_context, new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 * 1024 * 1024 }),
            NullLogger<CustomAreaService>.Instance, maxLocations, maxEdgeCandidates);

    private async Task<Location> AddLocationAsync(string name, string wkt, params Area[] areas)
    {
        var geometry = new WKTReader().Read(wkt);
        geometry.SRID = Srid;

        var location = new Location
        {
            LookupId = LookupPrefix + name,
            East = (int)geometry.Coordinates[0].X,
            North = (int)geometry.Coordinates[0].Y,
            Locality = LookupPrefix + name,
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
            Geometry = geometry,
            Areas = areas,
        };

        _context.Set<Location>().Add(location);
        await _context.SaveChangesAsync();
        return location;
    }
}

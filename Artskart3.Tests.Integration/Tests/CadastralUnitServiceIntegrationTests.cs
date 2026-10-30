using System.Net;
using System.Text;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.IO;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Snittet mot Location.Geometry kjøres i SQL Server. Enhetstestene går mot InMemory,
/// som verken oversetter STIntersects eller avviser ugyldige geometrier.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class CadastralUnitServiceIntegrationTests : IAsyncLifetime
{
    // Samme SRID som de andre testene bruker på Location.Geometry. Tabellen skal ha én.
    private const int Srid = 25833;
    private const string LookupPrefix = "cadastral-";

    // To teiger som overlapper mellom 900 100 og 900 150 øst. Som én MultiPolygon er de en
    // ugyldig geometri, og den avviser SQL Server.
    private const string OverlappingParcels = """
        {
          "type": "FeatureCollection",
          "features": [
            { "type": "Feature", "geometry": { "type": "Polygon", "coordinates": [[[900000,7900000],[900150,7900000],[900150,7900100],[900000,7900100],[900000,7900000]]] } },
            { "type": "Feature", "geometry": { "type": "Polygon", "coordinates": [[[900100,7900000],[900250,7900000],[900250,7900100],[900100,7900100],[900100,7900000]]] } }
          ]
        }
        """;

    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    public CadastralUnitServiceIntegrationTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _context = new ArtskartDbContext(options);
        await _context.Set<Location>().Where(l => l.LookupId!.StartsWith(LookupPrefix)).ExecuteDeleteAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetLocationIdsAsync_FindsLocationsWhoseGeometryIntersectsTheProperty()
    {
        var inside = await AddLocationAsync("inside", "POINT (900050 7900050)");
        var inOverlap = await AddLocationAsync("overlap", "POINT (900125 7900050)");
        await AddLocationAsync("outside", "POINT (900400 7900050)");

        // Første punkt, og dermed East/North, ligger utenfor eiendommen for disse to.
        var areaReachingIn = await AddLocationAsync(
            "area", "POLYGON ((900400 7900300, 900240 7900300, 900240 7900090, 900400 7900090, 900400 7900300))");
        var lineCrossing = await AddLocationAsync("line", "LINESTRING (899900 7900050, 900400 7900050)");

        var sut = new CadastralUnitService(
            new HttpClient(new StaticResponseHandler(OverlappingParcels)) { BaseAddress = new Uri("https://kartverket.test/eiendom/v1/") },
            _context,
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 * 1024 * 1024 }));

        var result = await sut.GetLocationIdsAsync(["1101-1/2"]);

        result.Should().BeEquivalentTo([inside.Id, inOverlap.Id, areaReachingIn.Id, lineCrossing.Id]);
    }

    private async Task<Location> AddLocationAsync(string name, string wkt)
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
        };

        _context.Set<Location>().Add(location);
        await _context.SaveChangesAsync();
        return location;
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}

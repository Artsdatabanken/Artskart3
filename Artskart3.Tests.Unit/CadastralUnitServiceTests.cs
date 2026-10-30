using System.Net;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using NetTopologySuite.IO;
using Location = Artskart3.Core.Domain.Entities.Location;

namespace Artskart3.Tests.Unit;

public class CadastralUnitServiceTests
{
    // To teiger i EPSG:25833: en trekant med hjørner i (0,0), (100,0) og (0,100), og et
    // kvadrat fra (200,200) til (210,210) levert som MultiPolygon.
    private const string TwoParcels = """
        {
          "type": "FeatureCollection",
          "features": [
            { "type": "Feature", "geometry": { "type": "Polygon", "coordinates": [[[0,0],[100,0],[0,100],[0,0]]] } },
            { "type": "Feature", "geometry": { "type": "MultiPolygon", "coordinates": [[[[200,200],[210,200],[210,210],[200,210],[200,200]]]] } }
          ]
        }
        """;

    private const string NoParcels = """{ "type": "FeatureCollection", "features": [] }""";

    [Fact]
    public async Task GetAsync_ReturnsAllParcelsAsOneMultiPolygon()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, TwoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context);

        var result = await sut.GetAsync("1101-1/2");

        result.Should().NotBeNull();
        result!.Id.Should().Be("1101-1/2");
        var geometry = new WKTReader().Read(result.WktPolygon);
        geometry.GeometryType.Should().Be("MultiPolygon");
        geometry.NumGeometries.Should().Be(2);
        result.Extent.Should().BeEquivalentTo(new { MinX = 0, MinY = 0, MaxX = 210, MaxY = 210 });
    }

    [Fact]
    public async Task GetAsync_AsksKartverketForAreaInUtm33()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, TwoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context);

        await sut.GetAsync(" 1101 - 01/2 ");

        handler.LastRequestUri!.Query.Should().Be("?matrikkelnummer=1101-1%2F2&omrade=true&utkoordsys=25833");
    }

    [Fact]
    public async Task GetAsync_WhenKartverketFindsNothing_ReturnsNull()
    {
        using var context = CreateInMemoryContext();
        var sut = CreateSut(new CountingHandler(HttpStatusCode.OK, NoParcels), context);

        var result = await sut.GetAsync("1101-99999/1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenKartverketFindsNothing_AsksAgainAfterTenMinutes()
    {
        var clock = new ManualClock();
        var handler = new CountingHandler(HttpStatusCode.OK, NoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context, CreateCache(clock));

        await sut.GetAsync("1101-99999/1");
        clock.UtcNow += TimeSpan.FromMinutes(11);
        await sut.GetAsync("1101-99999/1");

        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenKartverketFindsTheProperty_KeepsItLongerThanTenMinutes()
    {
        var clock = new ManualClock();
        var handler = new CountingHandler(HttpStatusCode.OK, TwoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context, CreateCache(clock));

        await sut.GetAsync("1101-1/2");
        clock.UtcNow += TimeSpan.FromMinutes(11);
        await sut.GetAsync("1101-1/2");

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_ConcurrentLookupsOfTheSameNumber_AskKartverketOnce()
    {
        var handler = new GatedHandler(TwoParcels);
        var cache = CreateCache();
        using var context = CreateInMemoryContext();

        // Én tjeneste per kall og en delt buffer, som i API-et.
        var lookups = Enumerable.Range(0, 5).Select(_ => CreateSut(handler, context, cache).GetAsync("1101-3/4")).ToArray();
        handler.Open();
        await Task.WhenAll(lookups);

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenKartverketFails_Throws()
    {
        using var context = CreateInMemoryContext();
        var sut = CreateSut(new CountingHandler(HttpStatusCode.InternalServerError, ""), context);

        var act = () => sut.GetAsync("1101-1/2");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetLocationIdsAsync_ReturnsLocationsWhoseGeometryIntersectsAParcel()
    {
        using var context = CreateInMemoryContext();
        context.Set<Location>().AddRange(
            CreateLocation(1, "POINT (10 10)"),                                           // i trekanten
            CreateLocation(2, "POINT (90 90)"),                                           // i trekantens envelope, men utenfor trekanten
            CreateLocation(3, "POINT (205 205)"),                                         // i kvadratet
            CreateLocation(4, "POINT (0 50)"),                                            // på kanten av trekanten
            CreateLocation(5, "POINT (500 500)"),                                         // langt unna
            CreateLocation(6, "POLYGON ((150 150, 202 150, 202 202, 150 202, 150 150))"), // flate som når inn i kvadratet
            CreateLocation(7, "LINESTRING (-50 20, 150 20)"),                             // linje gjennom trekanten
            CreateLocation(8, wkt: null));                                                // uten geometri
        await context.SaveChangesAsync();
        var sut = CreateSut(new CountingHandler(HttpStatusCode.OK, TwoParcels), context);

        var result = await sut.GetLocationIdsAsync(["1101-1/2"]);

        result.Should().BeEquivalentTo([1, 3, 4, 6, 7]);
    }

    [Fact]
    public async Task GetLocationIdsAsync_AsksKartverketOncePerNumber()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, TwoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context);

        await sut.GetLocationIdsAsync(["1101-1/2"]);
        await sut.GetLocationIdsAsync(["1101-01/02"]);
        await sut.GetAsync("1101-1/2");

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetLocationIdsAsync_IgnoresInvalidNumbers()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, TwoParcels);
        using var context = CreateInMemoryContext();
        var sut = CreateSut(handler, context);

        var result = await sut.GetLocationIdsAsync(["abc"]);

        result.Should().BeEmpty();
        handler.RequestCount.Should().Be(0);
    }

    private static CadastralUnitService CreateSut(HttpMessageHandler handler, ArtskartDbContext context, IMemoryCache? cache = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://kartverket.test/eiendom/v1/") },
            context,
            cache ?? CreateCache());

    // Samme grense som den delte bufferen i API-et: da må hver post oppgi Size.
    private static MemoryCache CreateCache(ISystemClock? clock = null) =>
        new(new MemoryCacheOptions { SizeLimit = 128 * 1024 * 1024, Clock = clock });

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    // East/North er geometriens første punkt, så for flater og linjer ligger det utenfor eiendommen.
    private static Location CreateLocation(int id, string? wkt)
    {
        var geometry = wkt == null ? null : new WKTReader().Read(wkt);
        var first = geometry?.Coordinates[0];
        return new()
        {
            Id = id,
            East = (int)(first?.X ?? 0),
            North = (int)(first?.Y ?? 0),
            Geometry = geometry,
            TimeStamp = DateTime.UtcNow,
            NodeId = 1,
        };
    }

    private sealed class CountingHandler(HttpStatusCode statusCode, string responseContent) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    // Holder svaret tilbake til testen åpner, så alle kallene rekker å bomme på bufferen først.
    private sealed class GatedHandler(string responseContent) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public int RequestCount => _requestCount;

        public void Open() => _gate.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            await _gate.Task;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ManualClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}

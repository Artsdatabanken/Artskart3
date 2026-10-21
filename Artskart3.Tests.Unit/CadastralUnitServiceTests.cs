using System.Net;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    public async Task GetAsync_WhenKartverketFails_Throws()
    {
        using var context = CreateInMemoryContext();
        var sut = CreateSut(new CountingHandler(HttpStatusCode.InternalServerError, ""), context);

        var act = () => sut.GetAsync("1101-1/2");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetLocationIdsAsync_ReturnsOnlyLocationsInsideAParcel()
    {
        using var context = CreateInMemoryContext();
        context.Set<Location>().AddRange(
            CreateLocation(1, east: 10, north: 10),    // i trekanten
            CreateLocation(2, east: 90, north: 90),    // i trekantens envelope, men utenfor trekanten
            CreateLocation(3, east: 205, north: 205),  // i kvadratet
            CreateLocation(4, east: 0, north: 50),     // på kanten av trekanten
            CreateLocation(5, east: 500, north: 500)); // langt unna
        await context.SaveChangesAsync();
        var sut = CreateSut(new CountingHandler(HttpStatusCode.OK, TwoParcels), context);

        var result = await sut.GetLocationIdsAsync(["1101-1/2"]);

        result.Should().BeEquivalentTo([1, 3, 4]);
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

    private static CadastralUnitService CreateSut(HttpMessageHandler handler, ArtskartDbContext context) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://kartverket.test/eiendom/v1/") },
            context,
            new MemoryCache(new MemoryCacheOptions()));

    private static ArtskartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }

    private static Location CreateLocation(int id, int east, int north) =>
        new() { Id = id, East = east, North = north, TimeStamp = DateTime.UtcNow, NodeId = 1 };

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
}

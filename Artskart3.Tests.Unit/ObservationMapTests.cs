using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace Artskart3.Tests.Unit;

public class ObservationMapTests
{
    private static readonly ObservationPointDto Point = new(60, 10, 200000, 6500000);
    private const int Center = ObservationMapService.LocalSize / 2;

    [Fact]
    public void LocalLayout_Shows5KmAroundThePoint()
    {
        var layout = ObservationMapService.GetLocalLayout(Subject(Point));
        layout.Extent.Should().Equal(197500, 6497500, 202500, 6502500);
        layout.ShowShape.Should().BeFalse();
        layout.RingRadius.Should().Be(0);
    }

    [Theory]
    [InlineData(500, 2500, 80)]
    [InlineData(2000, 2500, 320)]
    [InlineData(15000, 18000, 333.333)]
    [InlineData(50, 2500, 0)]
    [InlineData(220000, 250000, 352)]
    [InlineData(300000, 250000, 480)]
    public void LocalLayout_FitsPrecisionRingLargerThanTheMarker(int precision, double halfSize, float radius)
    {
        var layout = ObservationMapService.GetLocalLayout(Subject(Point, precision));
        layout.Extent.Should().Equal(Point.East - halfSize, Point.North - halfSize, Point.East + halfSize, Point.North + halfSize);
        layout.ShowShape.Should().BeFalse();
        layout.RingRadius.Should().BeApproximately(radius, 0.001f);
    }

    [Fact]
    public void LocalLayout_CentresOnShapeAndDropsRing()
    {
        var layout = ObservationMapService.GetLocalLayout(Subject(Point, 5000, Square(230000, 6520000, 15000)));
        layout.Extent.Should().Equal(212000, 6502000, 248000, 6538000);
        layout.ShowShape.Should().BeTrue();
        layout.RingRadius.Should().Be(0);
    }

    [Fact]
    public void LocalLayout_ShowsShapeSmallerThanTheMarkerAsThePoint()
    {
        var layout = ObservationMapService.GetLocalLayout(Subject(Point, 5000, Square(200050, 6500050, 50)));
        layout.Extent.Should().Equal(197500, 6497500, 202500, 6502500);
        layout.ShowShape.Should().BeFalse();
        layout.RingRadius.Should().Be(0);
    }

    [Fact]
    public void OverviewExtent_PreservesAspectRatioAndIncludesOutlyingPoint()
    {
        var point = new ObservationPointDto(71, -8, -800000, 7900000);
        var overview = ObservationMapService.GetOverviewExtent(point);
        ((overview[2] - overview[0]) / (overview[3] - overview[1])).Should().BeApproximately(0.625, 0.00001);
        point.East.Should().BeInRange((int)overview[0], (int)overview[2]);
        point.North.Should().BeInRange((int)overview[1], (int)overview[3]);
    }

    [Fact]
    public void Fingerprint_ChangesWithEverythingTheLocalMapIsDrawnFrom()
    {
        using var service = CreateService(new WmsHandler());
        var prints = new[]
        {
            Subject(Point), Subject(Point with { East = 200001 }), Subject(Point, 2000),
            Subject(Point, shape: Square(200000, 6500000, 3000)), Subject(Point, shape: Square(200000, 6500000, 3000), maskingSquare: true),
        }.Select(subject => service.Fingerprint(subject, false)).ToList();
        prints.Should().OnlyHaveUniqueItems();
        service.Fingerprint(Subject(Point, 2000), true).Should().Be(service.Fingerprint(Subject(Point), true));
    }

    [Theory]
    [InlineData(false, ObservationMapService.LocalSize, ObservationMapService.LocalSize)]
    [InlineData(true, ObservationMapService.OverviewWidth, ObservationMapService.OverviewHeight)]
    public async Task Renderer_ReturnsWebpWithCorrectDimensionsAndCaches(bool overview, int width, int height)
    {
        using var handler = new WmsHandler();
        using var service = CreateService(handler);
        var image = await service.RenderAsync(Subject(Point), overview, default);
        image.ContentType.Should().Be("image/webp");
        using var bitmap = Decode(image.Bytes);
        bitmap.Width.Should().Be(width);
        bitmap.Height.Should().Be(height);
        if (!overview) ShouldBeNear(bitmap.GetPixel(Center, Center), new SKColor(0, 90, 113));
        handler.Calls.Should().Be(3);
        (await service.RenderAsync(Subject(Point), overview, default)).Bytes.Should().BeSameAs(image.Bytes);
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Renderer_FetchesOverviewBackgroundOnce()
    {
        using var handler = new WmsHandler();
        using var service = CreateService(handler);
        var first = await service.RenderAsync(Subject(Point), true, default);
        var second = await service.RenderAsync(Subject(new ObservationPointDto(63, 10, 260000, 7000000)), true, default);
        second.Bytes.Should().NotEqual(first.Bytes);
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Renderer_DrawsEdgeOverviewsInParallelWithoutSharingTheirBackground()
    {
        using var handler = new WmsHandler { Release = new TaskCompletionSource() };
        using var service = CreateService(handler);
        // Fire funn, fordi to av dem kan havne på samme stripelås og da tegnes etter hverandre.
        var edgeFinds = new[] { 1_070_000, 1_080_000, 1_090_000, 1_100_000 }
            .Select(east => service.RenderAsync(Subject(new ObservationPointDto(69.7, 30, east, 7_780_000)), true, default))
            .ToList();
        await WaitUntilAsync(() => handler.Calls >= 6);
        handler.Release.SetResult();
        await Task.WhenAll(edgeFinds);

        await service.RenderAsync(Subject(Point), true, default);
        handler.Calls.Should().Be(15, "the default background is fetched on its own");
    }

    [Fact]
    public async Task Renderer_StopsTheOtherLayersWhenOneFails()
    {
        using var handler = new WmsHandler { DelayMilliseconds = 20_000, FailingLayer = "wms.terrengmodell" };
        using var service = CreateService(handler);
        var stopwatch = Stopwatch.StartNew();
        await service.Invoking(s => s.RenderAsync(Subject(Point), false, default)).Should().ThrowAsync<HttpRequestException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Renderer_DrawsPrecisionRingAroundTheMarker()
    {
        using var service = CreateService(new WmsHandler());
        using var bitmap = Decode((await service.RenderAsync(Subject(Point, 500), false, default)).Bytes);
        ShouldBeNear(bitmap.GetPixel(Center, Center), new SKColor(0, 90, 113));
        ShouldBeTinted(bitmap.GetPixel(Center + 50, Center));
        ShouldBeNear(bitmap.GetPixel(Center + 80, Center), new SKColor(0, 90, 113), OutlineTolerance);
        ShouldBeNear(bitmap.GetPixel(Center + 120, Center), SKColors.White);
    }

    [Fact]
    public async Task Renderer_DrawsShapeInsteadOfTheMarker()
    {
        using var service = CreateService(new WmsHandler());
        using var bitmap = Decode((await service.RenderAsync(Subject(Point, 2000, Square(200000, 6500000, 750)), false, default)).Bytes);
        ShouldBeTinted(bitmap.GetPixel(Center, Center));
        ShouldBeNear(bitmap.GetPixel(Center + 120, Center), new SKColor(0, 90, 113), OutlineTolerance);
        ShouldBeNear(bitmap.GetPixel(Center + 200, Center), SKColors.White);
    }

    [Fact]
    public async Task Renderer_RejectsXmlExceptionEvenWithHttp200AndDoesNotCacheFailure()
    {
        using var handler = new WmsHandler { XmlError = true };
        using var service = CreateService(handler);
        Func<Task> render = () => service.RenderAsync(Subject(Point), false, default);
        await render.Should().ThrowAsync<InvalidDataException>();
        handler.XmlError = false;
        (await service.RenderAsync(Subject(Point), false, default)).Bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Renderer_CancelsSupersededWorkWithoutFailingAnotherCaller()
    {
        using var handler = new WmsHandler { DelayMilliseconds = 25 };
        using var service = CreateService(handler);
        using var cancellation = new CancellationTokenSource();
        var first = service.RenderAsync(Subject(Point), false, cancellation.Token);
        var second = service.RenderAsync(Subject(Point), false, default);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        (await second).Bytes.Should().NotBeEmpty();
        handler.Calls.Should().Be(6);
    }

    [Fact]
    public async Task Renderer_RejectsRendersBeyondTheQueueAndServesTheRest()
    {
        using var handler = new WmsHandler { Release = new TaskCompletionSource() };
        using var service = CreateService(handler);
        var renders = Enumerable.Range(0, 64)
            .Select(i => service.RenderAsync(Subject(Point with { East = 200000 + i }), false, default))
            .ToList();
        var firstDone = await Task.WhenAny(renders);
        await firstDone.Invoking(task => task).Should().ThrowAsync<ObservationMapBusyException>();

        handler.Release.SetResult();
        var served = await Task.WhenAll(renders.Select(async render =>
        {
            try
            {
                await render;
                return true;
            }
            catch (ObservationMapBusyException)
            {
                return false;
            }
        }));
        served.Count(ok => ok).Should().Be(20, "four render at once and sixteen wait");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        condition().Should().BeTrue();
    }

    private static ObservationMapSubject Subject(ObservationPointDto point, int? precision = null, Geometry? shape = null, bool maskingSquare = false) =>
        new(point, precision, shape, maskingSquare);

    private static Polygon Square(double x, double y, double half) => new GeometryFactory().CreatePolygon(
    [
        new(x - half, y - half), new(x + half, y - half), new(x + half, y + half), new(x - half, y + half), new(x - half, y - half),
    ]);

    private static ObservationMapService CreateService(WmsHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ObservationMapService.ClientName)).Returns(() => new HttpClient(handler, false));
        return new ObservationMapService(factory.Object);
    }

    private static SKBitmap Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Webp);
        return SKBitmap.Decode(codec);
    }

    // WebP er tapsbeheftet og lagrer fargen med halv oppløsning, så tynne linjer avviker mest.
    private const int OutlineTolerance = 40;

    private static void ShouldBeNear(SKColor actual, SKColor expected, int tolerance = 16)
    {
        Math.Abs(actual.Red - expected.Red).Should().BeLessThan(tolerance, $"{actual} ≈ {expected}");
        Math.Abs(actual.Green - expected.Green).Should().BeLessThan(tolerance, $"{actual} ≈ {expected}");
        Math.Abs(actual.Blue - expected.Blue).Should().BeLessThan(tolerance, $"{actual} ≈ {expected}");
    }

    private static void ShouldBeTinted(SKColor actual) =>
        actual.Red.Should().BeInRange((byte)200, (byte)235, $"{actual} is the translucent area fill over white");

    private sealed class WmsHandler : HttpMessageHandler
    {
        private int calls;
        public int Calls => calls;
        public bool XmlError { get; set; }
        public int DelayMilliseconds { get; set; }
        public TaskCompletionSource? Release { get; init; }
        public string? FailingLayer { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            if (FailingLayer is not null && request.RequestUri!.AbsolutePath.Contains(FailingLayer))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            await Task.Delay(DelayMilliseconds, cancellationToken);
            if (Release is not null) await Release.Task.WaitAsync(cancellationToken);
            if (XmlError)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ServiceException/>") };
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            query["SRS"].ToString().Should().Be("EPSG:25833");
            using var surface = SKSurface.Create(new SKImageInfo(int.Parse(query["WIDTH"].ToString()), int.Parse(query["HEIGHT"].ToString())));
            surface.Canvas.Clear(SKColors.Transparent);
            using var snapshot = surface.Snapshot();
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png.ToArray()) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        }
    }
}

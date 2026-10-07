using System.Net;
using System.Net.Http.Headers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using SkiaSharp;

namespace Artskart3.Tests.Unit;

public class ObservationMapTests
{
    [Fact]
    public void Extents_PreserveScaleAspectRatioAndOutlyingPoint()
    {
        var point = new ObservationPointDto(71, -8, -800000, 7900000);
        var local = ObservationMapService.GetExtent(point, false);
        (local[2] - local[0]).Should().BeApproximately(82149.2, 0.001);
        ((local[0] + local[2]) / 2).Should().Be(point.East);
        var overview = ObservationMapService.GetExtent(point, true);
        ((overview[2] - overview[0]) / (overview[3] - overview[1])).Should().BeApproximately(0.625, 0.00001);
        point.East.Should().BeInRange((int)overview[0], (int)overview[2]);
        point.North.Should().BeInRange((int)overview[1], (int)overview[3]);
    }

    [Theory]
    [InlineData(false, 800)]
    [InlineData(true, 500)]
    public async Task Renderer_ReturnsRealPngWithCorrectDimensionsAndCaches(bool overview, int width)
    {
        using var handler = new WmsHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ObservationMapService.ClientName)).Returns(() => new HttpClient(handler, false));
        using var service = new ObservationMapService(factory.Object);
        var point = new ObservationPointDto(60, 10, 200000, 6500000);
        var bytes = await service.RenderAsync(point, overview, default);
        using var bitmap = SKBitmap.Decode(bytes);
        bitmap.Width.Should().Be(width);
        bitmap.Height.Should().Be(800);
        if (!overview) bitmap.GetPixel(400, 400).Should().Be(new SKColor(0, 90, 113));
        handler.Calls.Should().Be(3);
        (await service.RenderAsync(point, overview, default)).Should().BeSameAs(bytes);
        handler.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Renderer_RejectsXmlExceptionEvenWithHttp200AndDoesNotCacheFailure()
    {
        using var handler = new WmsHandler { XmlError = true };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ObservationMapService.ClientName)).Returns(() => new HttpClient(handler, false));
        using var service = new ObservationMapService(factory.Object);
        var point = new ObservationPointDto(60, 10, 200000, 6500000);
        Func<Task> render = () => service.RenderAsync(point, false, default);
        await render.Should().ThrowAsync<InvalidDataException>();
        handler.XmlError = false;
        (await service.RenderAsync(point, false, default)).Should().NotBeEmpty();
    }

    private sealed class WmsHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool XmlError { get; set; }
        public int DelayMilliseconds { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(DelayMilliseconds, cancellationToken);
            if (XmlError)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ServiceException/>") };
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            query["SRS"].ToString().Should().Be("EPSG:25833");
            using var surface = SKSurface.Create(new SKImageInfo(int.Parse(query["WIDTH"].ToString()), int.Parse(query["HEIGHT"].ToString())));
            surface.Canvas.Clear(SKColors.White);
            using var snapshot = surface.Snapshot();
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png.ToArray()) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        }
    }

    [Fact]
    public async Task Renderer_CancelsSupersededWorkWithoutFailingAnotherCaller()
    {
        using var handler = new WmsHandler { DelayMilliseconds = 25 };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ObservationMapService.ClientName)).Returns(() => new HttpClient(handler, false));
        using var service = new ObservationMapService(factory.Object);
        using var cancellation = new CancellationTokenSource();
        var point = new ObservationPointDto(60, 10, 200000, 6500000);
        var first = service.RenderAsync(point, false, cancellation.Token);
        var second = service.RenderAsync(point, false, default);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        (await second).Should().NotBeEmpty();
        handler.Calls.Should().Be(4);
    }
}

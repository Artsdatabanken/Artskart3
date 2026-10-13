using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artskart3.Tests.Unit;

public class ObservationDetailsTests
{
    [Fact]
    public async Task Service_PreservesPublicDataAndRejectsInvalidUrls()
    {
        var repository = new Mock<IObservationDetailsRepository>();
        var detail = new ObservationDetailDto
        {
            Id = 42, Point = new(60, 10, 200000, 6500000), AssessmentUrl = "javascript:alert(1)",
            Images = [new() { Id = 1, Origin = "file:///tmp/image.png", HasStoredImage = true }],
        };
        repository.Setup(r => r.GetAsync(42, default)).ReturnsAsync(detail);
        var service = new ObservationDetailsService(repository.Object, NullLogger<ObservationDetailsService>.Instance);
        var result = await service.GetAsync(42, default);
        result!.Point.Should().Be(detail.Point);
        result.AssessmentUrl.Should().BeNull();
        result.Images[0].Origin.Should().BeNull();
        result.Images[0].HasStoredImage.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(91, 10, 6500000)]
    [InlineData(60, 181, 6500000)]
    [InlineData(60, 10, 0)]
    public async Task Service_DoesNotFabricateMissingCoordinates(double latitude, double longitude, int north)
    {
        var repository = new Mock<IObservationDetailsRepository>();
        repository.Setup(r => r.GetAsync(42, default)).ReturnsAsync(new ObservationDetailDto { Id = 42, Point = new(latitude, longitude, 200000, north) });
        var service = new ObservationDetailsService(repository.Object, NullLogger<ObservationDetailsService>.Instance);
        (await service.GetAsync(42, default))!.Point.Should().BeNull();
    }

    [Fact]
    public async Task Controller_ReturnsNotFoundAndRejectsInvalidIds()
    {
        var details = new Mock<IObservationDetailsService>();
        var maps = new Mock<IObservationMapService>();
        var controller = CreateController(details, maps);
        (await controller.Get(42, default)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await controller.Get(0, default)).Result.Should().BeOfType<BadRequestObjectResult>();
        (await controller.Map(42, "arbitrary", default)).Should().BeOfType<BadRequestObjectResult>();
        maps.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controller_MapsRequirePublicCoordinatesAndSurfaceUpstreamFailures()
    {
        var details = new Mock<IObservationDetailsService>();
        var maps = new Mock<IObservationMapService>();
        details.Setup(d => d.GetAsync(42, default)).ReturnsAsync(new ObservationDetailDto { Id = 42 });
        var controller = CreateController(details, maps);
        (await controller.Map(42, "local", default)).Should().BeOfType<UnprocessableEntityObjectResult>();
        var point = new ObservationPointDto(60, 10, 200000, 6500000);
        details.Setup(d => d.GetAsync(42, default)).ReturnsAsync(new ObservationDetailDto { Id = 42, Point = point });
        maps.Setup(m => m.RenderAsync(point, false, default)).ThrowsAsync(new InvalidDataException("WMS exception"));
        (await controller.Map(42, "local", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task Controller_ImagesAreSeparateAndSupportConditionalRequests()
    {
        var details = new Mock<IObservationDetailsService>();
        details.Setup(d => d.GetImageAsync(42, 3, default)).ReturnsAsync(new ObservationImageFile([1, 2, 3], "image/png"));
        var controller = CreateController(details, new Mock<IObservationMapService>());
        var response = await controller.Image(42, 3, default);
        response.Should().BeOfType<FileContentResult>().Which.ContentType.Should().Be("image/png");
        controller.Request.Headers.IfNoneMatch = controller.Response.Headers.ETag;
        (await controller.Image(42, 3, default)).Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(304);
        details.Verify(d => d.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ObservationsController CreateController(Mock<IObservationDetailsService> details, Mock<IObservationMapService> maps) =>
        new(details.Object, maps.Object, NullLogger<ObservationsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}

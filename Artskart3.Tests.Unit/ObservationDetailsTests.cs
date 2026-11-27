using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.IO;

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
    public async Task Controller_ResolvesTrimmedOccurrenceIdAndRejectsBlankValues()
    {
        var details = new Mock<IObservationDetailsService>();
        details.Setup(d => d.FindIdByOccurrenceIdAsync("urn:catalog:O:L:37", default)).ReturnsAsync(42);
        var controller = CreateController(details, new Mock<IObservationMapService>());
        (await controller.FindByOccurrenceId(" urn:catalog:O:L:37 ", default)).Result
            .Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(42);
        (await controller.FindByOccurrenceId("urn:catalog:O:L:38", default)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await controller.FindByOccurrenceId("  ", default)).Result.Should().BeOfType<BadRequestObjectResult>();
        (await controller.FindByOccurrenceId(null, default)).Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Repository_ResolvesOccurrenceIdToLowestLiveObservation()
    {
        await using var context = CreateInMemoryContext();
        context.Observations.AddRange(
            new Observation { Id = 3, OccurrenceId = "urn:catalog:O:L:37" },
            new Observation { Id = 1, OccurrenceId = "urn:catalog:O:L:37", IsDeleted = true },
            new Observation { Id = 2, OccurrenceId = "urn:catalog:O:L:37" },
            new Observation { Id = 4, OccurrenceId = "urn:catalog:O:L:38", IsDeleted = true });
        await context.SaveChangesAsync();
        var repository = new ObservationDetailsRepository(context, NullLogger<ObservationDetailsRepository>.Instance);

        (await repository.FindIdByOccurrenceIdAsync("urn:catalog:O:L:37", default)).Should().Be(2);
        (await repository.FindIdByOccurrenceIdAsync("urn:catalog:O:L:38", default)).Should().BeNull();
        (await repository.FindIdByOccurrenceIdAsync(new string('x', 256), default)).Should().BeNull();
    }

    [Fact]
    public async Task Repository_FallsBackToProxyIdOnlyWhenNoOccurrenceIdMatches()
    {
        await using var context = CreateInMemoryContext();
        context.Observations.AddRange(
            new Observation { Id = 7, OccurrenceId = "0008EF28-6C98", ProxyId = "nina/skandobs/0008ef28-6c98" },
            new Observation { Id = 8, ProxyId = "shared" },
            new Observation { Id = 9, OccurrenceId = "shared" },
            new Observation { Id = 10, ProxyId = "nina/skandobs/deleted", IsDeleted = true });
        await context.SaveChangesAsync();
        var repository = new ObservationDetailsRepository(context, NullLogger<ObservationDetailsRepository>.Instance);

        (await repository.FindIdByOccurrenceIdAsync("nina/skandobs/0008ef28-6c98", default)).Should().Be(7);
        (await repository.FindIdByOccurrenceIdAsync("shared", default)).Should().Be(9);
        (await repository.FindIdByOccurrenceIdAsync("nina/skandobs/deleted", default)).Should().BeNull();
    }

    private static ArtskartDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<ArtskartDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Controller_MapsRequirePublicCoordinatesAndSurfaceUpstreamFailures()
    {
        var details = new Mock<IObservationDetailsService>();
        var maps = new Mock<IObservationMapService>();
        var controller = CreateController(details, maps);
        (await controller.Map(42, "local", default)).Should().BeOfType<NotFoundObjectResult>();
        details.Setup(d => d.GetMapSubjectAsync(42, default)).ReturnsAsync(new ObservationMapSubject(null, null, null, false));
        (await controller.Map(42, "local", default)).Should().BeOfType<UnprocessableEntityObjectResult>();

        var subject = new ObservationMapSubject(new(60, 10, 200000, 6500000), null, null, false);
        details.Setup(d => d.GetMapSubjectAsync(42, default)).ReturnsAsync(subject);
        maps.Setup(m => m.Fingerprint(subject, false)).Returns("ABC");
        maps.Setup(m => m.RenderAsync(subject, false, default)).ThrowsAsync(new InvalidDataException("WMS exception"));
        (await controller.Map(42, "local", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
        maps.Setup(m => m.RenderAsync(subject, false, default)).ThrowsAsync(new ObservationMapBusyException());
        (await controller.Map(42, "local", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
        controller.Response.Headers.CacheControl.Should().BeEmpty("failures must not be cached");
    }

    [Fact]
    public async Task Controller_MapAnswersNotModifiedWithoutRendering()
    {
        var details = new Mock<IObservationDetailsService>();
        var maps = new Mock<IObservationMapService>();
        var subject = new ObservationMapSubject(new(60, 10, 200000, 6500000), 25, null, false);
        details.Setup(d => d.GetMapSubjectAsync(42, default)).ReturnsAsync(subject);
        maps.Setup(m => m.Fingerprint(subject, true)).Returns("ABC");
        maps.Setup(m => m.RenderAsync(subject, true, default)).ReturnsAsync(new ObservationImageFile([1, 2, 3], "image/webp"));
        var controller = CreateController(details, maps);

        (await controller.Map(42, "overview", default)).Should().BeOfType<FileContentResult>()
            .Which.ContentType.Should().Be("image/webp");
        controller.Response.Headers.ETag.ToString().Should().Be("W/\"ABC\"");
        controller.Response.Headers.CacheControl.ToString().Should().Be("public,max-age=300");
        foreach (var current in new[] { "W/\"ABC\"", "\"ABC\"", "\"OLD\", W/\"ABC\"" })
        {
            controller.Request.Headers.IfNoneMatch = current;
            (await controller.Map(42, "overview", default)).Should().BeOfType<StatusCodeResult>()
                .Which.StatusCode.Should().Be(304, current);
        }
        maps.Verify(m => m.RenderAsync(subject, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("POLYGON ((0 0, 1000 0, 1000 1000, 0 1000, 0 0))", LocationGeometryType.Polygon, "rute", false, false)]
    [InlineData("POLYGON ((0 0, 1000 0, 1000 1000, 0 1000, 0 0))", LocationGeometryType.Polygon, "1234_sensitiv", true, true)]
    [InlineData("POLYGON ((0 0, 1000 0, 1500 500, 1000 1000, 0 1000, 0 0))", LocationGeometryType.Polygon, null, true, false)]
    [InlineData("MULTILINESTRING ((0 0, 100 0, 200 0, 300 0, 400 0))", LocationGeometryType.MultiLineString, null, true, false)]
    [InlineData("POINT (0 0)", LocationGeometryType.Point, null, false, false)]
    [InlineData("GEOMETRYCOLLECTION (POINT (0 0), LINESTRING (0 0, 1 1))", LocationGeometryType.GeometryCollection, null, false, false)]
    public async Task Service_MapShowsTheShapesTheMainMapDraws(string wkt, LocationGeometryType type, string? lookupId, bool drawn, bool maskingSquare)
    {
        var repository = new Mock<IObservationDetailsRepository>();
        var geometry = new WKTReader().Read(wkt);
        repository.Setup(r => r.GetMapLocationAsync(42, default))
            .ReturnsAsync(new ObservationMapLocation(new(60, 10, 200000, 6500000), 25, type, geometry, lookupId));
        var service = new ObservationDetailsService(repository.Object, NullLogger<ObservationDetailsService>.Instance);
        var subject = await service.GetMapSubjectAsync(42, default);
        subject!.Shape.Should().Be(drawn ? geometry : null);
        subject.ShapeIsMaskingSquare.Should().Be(maskingSquare);
        subject.CoordinatePrecision.Should().Be(25);
    }

    [Fact]
    public async Task Service_MapSubjectDoesNotFabricateMissingCoordinates()
    {
        var repository = new Mock<IObservationDetailsRepository>();
        repository.Setup(r => r.GetMapLocationAsync(42, default))
            .ReturnsAsync(new ObservationMapLocation(new(0, 0, 0, 0), null, null, null, null));
        var service = new ObservationDetailsService(repository.Object, NullLogger<ObservationDetailsService>.Instance);
        (await service.GetMapSubjectAsync(42, default))!.Point.Should().BeNull();
        (await service.GetMapSubjectAsync(43, default)).Should().BeNull();
    }

    [Fact]
    public async Task Controller_AnnotationsReturnNotFoundAndSurfaceUpstreamFailures()
    {
        var annotations = new Mock<IObservationAnnotationService>();
        var controller = CreateController(new Mock<IObservationDetailsService>(), new Mock<IObservationMapService>(), annotations);
        (await controller.Annotations(0, default)).Result.Should().BeOfType<BadRequestObjectResult>();
        (await controller.Annotations(42, default)).Result.Should().BeOfType<NotFoundObjectResult>();

        annotations.Setup(a => a.GetAsync(42, default)).ReturnsAsync([new ObservationAnnotationDto { Comment = "Forkastes" }]);
        (await controller.Annotations(42, default)).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<List<ObservationAnnotationDto>>().Which.Should().ContainSingle();

        annotations.Setup(a => a.GetAsync(42, default)).ThrowsAsync(new HttpRequestException("503"));
        (await controller.Annotations(42, default)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);

        annotations.Setup(a => a.GetAsync(42, default)).ThrowsAsync(new System.Text.Json.JsonException("<html>"));
        (await controller.Annotations(42, default)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);

        annotations.Setup(a => a.GetAsync(42, default)).ThrowsAsync(new TaskCanceledException("timeout"));
        (await controller.Annotations(42, default)).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(504);
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

    private static ObservationsController CreateController(
        Mock<IObservationDetailsService> details, Mock<IObservationMapService> maps, Mock<IObservationAnnotationService>? annotations = null) =>
        new(details.Object, maps.Object, annotations?.Object ?? new Mock<IObservationAnnotationService>().Object, NullLogger<ObservationsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}

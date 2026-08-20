using Artskart3.Api.Controllers;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchControllerTests
{
    private readonly Mock<ISearchService> _serviceMock;
    private readonly Mock<ISpeciesService> _speciesServiceMock;
    private readonly Mock<ILogger<SearchController>> _loggerMock;
    private readonly SearchController _sut;

    public SearchControllerTests()
    {
        _serviceMock = new Mock<ISearchService>();
        _speciesServiceMock = new Mock<ISpeciesService>();
        _loggerMock = new Mock<ILogger<SearchController>>();
        _sut = new SearchController(_serviceMock.Object, _speciesServiceMock.Object, _loggerMock.Object, Options.Create(new PaginationOptions()));

        var services = new ServiceCollection();
        services.AddMvcCore();
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    // -----------------------------------------------------------------------
    // SearchTaxons
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchTaxons_WithEmptyName_ReturnsBadRequest()
    {
        var result = await _sut.SearchTaxons("   ");

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SearchTaxons_WithNullName_ReturnsBadRequest()
    {
        var result = await _sut.SearchTaxons(null!);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    [InlineData(-5)]
    public async Task SearchTaxons_WithInvalidMaxCount_ReturnsBadRequest(int maxCount)
    {
        var result = await _sut.SearchTaxons("parus", maxCount);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SearchTaxons_WithValidRequest_ReturnsOkWithTaxons()
    {
        var taxons = new List<TaxonDto> { new() { Id = 1, ValidScientificName = "Parus major" } };
        _serviceMock.Setup(s => s.GetTaxonsAsync("parus", 20)).ReturnsAsync(taxons);

        var result = await _sut.SearchTaxons("parus", 20);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(taxons);
    }

    [Fact]
    public async Task SearchTaxons_WhenServiceThrowsApplicationException_Throws()
    {
        _serviceMock
            .Setup(s => s.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new ApplicationException("Service unavailable"));

        var act = () => _sut.SearchTaxons("parus");

        await act.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task SearchTaxons_WhenServiceThrowsUnexpectedException_Throws()
    {
        _serviceMock
            .Setup(s => s.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var act = () => _sut.SearchTaxons("parus");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // GetObservationLocations
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    [InlineData(-1)]
    public async Task GetObservationLocations_WithInvalidMaxResults_Returns400(int maxResults)
    {
        var filter = new LocationSearchFilterDto { MaxResults = maxResults };
        _sut.HttpContext.Response.Body = new MemoryStream();

        await _sut.GetObservationLocations(filter);

        _sut.HttpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetObservationLocations_WhenPrecisionFromExceedsPrecisionTo_Returns400()
    {
        var filter = new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 500, To = 100 }
        };
        _sut.HttpContext.Response.Body = new MemoryStream();

        await _sut.GetObservationLocations(filter);

        _sut.HttpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetObservationLocations_WithValidFilter_WritesJsonToResponseBody()
    {
        var locations = new List<LocationModel>
        {
            new() { Id = 1, Latitude = 59.9, Longitude = 10.7, ObservationCount = 3 }
        };
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);
        _sut.HttpContext.Response.Body = new MemoryStream();

        await _sut.GetObservationLocations(new LocationSearchFilterDto());

        _sut.HttpContext.Response.ContentType.Should().Be("application/json");
        _sut.HttpContext.Response.Body.Position = 0;
        var body = await new StreamReader(_sut.HttpContext.Response.Body).ReadToEndAsync();
        body.Should().Contain("\"locations\"");
        body.Should().Contain("\"epsg\"");
    }

    [Fact]
    public async Task GetObservationLocations_WithNullFilter_UsesDefaultsAndSucceeds()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocationModel>());
        _sut.HttpContext.Response.Body = new MemoryStream();

        await _sut.GetObservationLocations(null);

        _sut.HttpContext.Response.ContentType.Should().Be("application/json");
        _serviceMock.Verify(s => s.GetLocationsAsync(It.IsNotNull<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetObservationLocations_WhenServiceThrowsApplicationException_Throws()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("DB error"));
        _sut.HttpContext.Response.Body = new MemoryStream();

        var act = () => _sut.GetObservationLocations(new LocationSearchFilterDto());

        await act.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task GetObservationLocations_WhenServiceThrowsUnexpectedException_Throws()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected"));
        _sut.HttpContext.Response.Body = new MemoryStream();

        var act = () => _sut.GetObservationLocations(new LocationSearchFilterDto());

        await act.Should().ThrowAsync<Exception>();
    }

    // -----------------------------------------------------------------------
    // GetAreaCounts
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreaCounts_WithInvalidZoomLevel_ReturnsBadRequest()
    {
        var result = await _sut.GetAreaCounts(zoomLevel: 3);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAreaCounts_ReturnsCountsWithETag()
    {
        var counts = new[] { new AreaCountDto { Fid = "03", ObservationCount = 100 } };
        _serviceMock
            .Setup(s => s.GetAreaCountsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AreaCountsResultDto(counts, "\"abc123\""));

        var result = await _sut.GetAreaCounts(zoomLevel: 1);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        (okResult.Value as AreaCountDto[]).Should().HaveCount(1);
        _sut.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"abc123\"");
    }

    [Fact]
    public async Task GetAreaCounts_WithMatchingETag_Returns304()
    {
        var counts = new[] { new AreaCountDto { Fid = "03", ObservationCount = 100 } };
        _serviceMock
            .Setup(s => s.GetAreaCountsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AreaCountsResultDto(counts, "\"abc123\""));

        _sut.HttpContext.Request.Headers["If-None-Match"] = "\"abc123\"";

        var result = await _sut.GetAreaCounts(zoomLevel: 1);

        var statusResult = result.Should().BeOfType<StatusCodeResult>().Subject;
        statusResult.StatusCode.Should().Be(304);
    }

    [Fact]
    public async Task GetAreaCounts_WithNonMatchingETag_Returns200()
    {
        var counts = new[] { new AreaCountDto { Fid = "03", ObservationCount = 100 } };
        _serviceMock
            .Setup(s => s.GetAreaCountsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AreaCountsResultDto(counts, "\"abc123\""));

        _sut.HttpContext.Request.Headers["If-None-Match"] = "\"old-etag\"";

        var result = await _sut.GetAreaCounts(zoomLevel: 1);

        result.Should().BeOfType<OkObjectResult>();
    }
}

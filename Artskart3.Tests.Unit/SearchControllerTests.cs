using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchControllerTests
{
    private readonly Mock<ISearchService> _serviceMock;
    private readonly Mock<ILogger<SearchController>> _loggerMock;
    private readonly SearchController _sut;

    public SearchControllerTests()
    {
        _serviceMock = new Mock<ISearchService>();
        _loggerMock = new Mock<ILogger<SearchController>>();
        _sut = new SearchController(_serviceMock.Object, _loggerMock.Object);
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
        var taxons = new List<Taxon> { new() { Id = 1, ValidScientificName = "Parus major" } };
        _serviceMock.Setup(s => s.GetTaxonsAsync("parus", 20)).ReturnsAsync(taxons);

        var result = await _sut.SearchTaxons("parus", 20);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(taxons);
    }

    [Fact]
    public async Task SearchTaxons_WhenServiceThrowsApplicationException_Returns503()
    {
        _serviceMock
            .Setup(s => s.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new ApplicationException("Service unavailable"));

        var result = await _sut.SearchTaxons("parus");

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SearchTaxons_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await _sut.SearchTaxons("parus");

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    // -----------------------------------------------------------------------
    // GetLocations
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    [InlineData(-1)]
    public async Task GetLocations_WithInvalidMaxResults_ReturnsBadRequest(int maxResults)
    {
        var filter = new LocationSearchFilterDto { MaxResults = maxResults };

        var result = await _sut.GetLocations(filter);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetLocations_WhenPrecisionFromExceedsPrecisionTo_ReturnsBadRequest()
    {
        var filter = new LocationSearchFilterDto
        {
            CoordinatePrecisionFrom = 500,
            CoordinatePrecisionTo = 100
        };

        var result = await _sut.GetLocations(filter);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetLocations_WithValidFilter_ReturnsContentResult()
    {
        var geoJson = """{"type":"FeatureCollection","features":[]}""";
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ReturnsAsync(geoJson);

        var result = await _sut.GetLocations(new LocationSearchFilterDto());

        result.Result.Should().BeOfType<ContentResult>()
            .Which.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetLocations_WithNullFilter_UsesDefaultsAndSucceeds()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ReturnsAsync("""{"type":"FeatureCollection","features":[]}""");

        var result = await _sut.GetLocations(null);

        result.Result.Should().BeOfType<ContentResult>();
    }

    [Fact]
    public async Task GetLocations_WhenServiceThrowsApplicationException_Returns503()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ThrowsAsync(new ApplicationException("DB error"));

        var result = await _sut.GetLocations(new LocationSearchFilterDto());

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetLocations_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ThrowsAsync(new Exception("Unexpected"));

        var result = await _sut.GetLocations(new LocationSearchFilterDto());

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    // -----------------------------------------------------------------------
    // GetAreas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreas_ReturnsOkWithAreaArray()
    {
        var areas = new List<AreaMarkerDto>
        {
            new() { Id = 1, Name = "Oslo", AreaTypeId = 2, ObservationCount = 500 }
        };
        _serviceMock.Setup(s => s.GetAreasByTypeIdsAsync(new[] { 1, 2 }, It.IsAny<CancellationToken>())).ReturnsAsync(areas);

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<AreaMarkerDto[]>()
            .Which.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAreas_WhenServiceThrowsApplicationException_Returns503()
    {
        _serviceMock
            .Setup(s => s.GetAreasByTypeIdsAsync(It.IsAny<int[]>()))
            .ThrowsAsync(new ApplicationException("Service error"));

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetAreas_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetAreasByTypeIdsAsync(It.IsAny<int[]>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }
}

using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchControllerAdditionalTests
{
    private readonly Mock<ISearchService> _serviceMock = new();
    private readonly Mock<ILogger<SearchController>> _loggerMock = new();

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSearchServiceIsNull()
    {
        var act = () => new SearchController(null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("searchService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new SearchController(_serviceMock.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task SearchTaxons_Returns200Ok_WithEmptyArrayWhenServiceReturnsEmpty()
    {
        var sut = CreateSut();
        _serviceMock.Setup(s => s.GetTaxonsAsync("tom", 20)).ReturnsAsync(Array.Empty<TaxonDto>());

        var result = await sut.SearchTaxons("tom");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<IEnumerable<TaxonDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetObservationLocations_Returns200_WhenOnlyCoordinatePrecisionFromIsSet()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ReturnsAsync("{\"type\":\"FeatureCollection\",\"features\":[]}");

        var result = await sut.GetObservationLocations(new LocationSearchFilterDto { CoordinatePrecisionFrom = 10, CoordinatePrecisionTo = 0 });

        result.Result.Should().BeOfType<ContentResult>();
        _serviceMock.Verify(s => s.GetLocationsAsync(It.Is<LocationSearchFilterDto>(f =>
            f.CoordinatePrecisionFrom == 10 &&
            f.CoordinatePrecisionTo == 0)), Times.Once);
    }

    [Fact]
    public async Task GetObservationLocations_Returns200_WhenOnlyCoordinatePrecisionToIsSet()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .ReturnsAsync("{\"type\":\"FeatureCollection\",\"features\":[]}");

        var result = await sut.GetObservationLocations(new LocationSearchFilterDto { CoordinatePrecisionFrom = 0, CoordinatePrecisionTo = 100 });

        result.Result.Should().BeOfType<ContentResult>();
        _serviceMock.Verify(s => s.GetLocationsAsync(It.Is<LocationSearchFilterDto>(f =>
            f.CoordinatePrecisionFrom == 0 &&
            f.CoordinatePrecisionTo == 100)), Times.Once);
    }

    private SearchController CreateSut() => new(_serviceMock.Object, _loggerMock.Object);
}
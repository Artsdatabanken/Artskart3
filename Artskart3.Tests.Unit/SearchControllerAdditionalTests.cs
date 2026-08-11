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

public class SearchControllerAdditionalTests
{
    private readonly Mock<ISearchService> _serviceMock = new();
    private readonly Mock<ISpeciesService> _speciesServiceMock = new();
    private readonly Mock<ILogger<SearchController>> _loggerMock = new();

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSearchServiceIsNull()
    {
        var act = () => new SearchController(null!, _speciesServiceMock.Object, _loggerMock.Object, Options.Create(new PaginationOptions()));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("searchService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSpeciesServiceIsNull()
    {
        var act = () => new SearchController(_serviceMock.Object, null!, _loggerMock.Object, Options.Create(new PaginationOptions()));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("speciesService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new SearchController(_serviceMock.Object, _speciesServiceMock.Object, null!, Options.Create(new PaginationOptions()));

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
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocationModel>());

        await sut.GetObservationLocations(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 10 } });

        sut.HttpContext.Response.ContentType.Should().Be("application/json");
        _serviceMock.Verify(s => s.GetLocationsAsync(It.Is<LocationSearchFilterDto>(f =>
            f.CoordinatePrecision != null &&
            f.CoordinatePrecision.From == 10 &&
            f.CoordinatePrecision.To == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetObservationLocations_Returns200_WhenOnlyCoordinatePrecisionToIsSet()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocationModel>());

        await sut.GetObservationLocations(new LocationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { To = 100 } });

        sut.HttpContext.Response.ContentType.Should().Be("application/json");
        _serviceMock.Verify(s => s.GetLocationsAsync(It.Is<LocationSearchFilterDto>(f =>
            f.CoordinatePrecision != null &&
            f.CoordinatePrecision.From == null &&
            f.CoordinatePrecision.To == 100), It.IsAny<CancellationToken>()), Times.Once);
    }

    private SearchController CreateSut()
    {
        var controller = new SearchController(_serviceMock.Object, _speciesServiceMock.Object, _loggerMock.Object, Options.Create(new PaginationOptions()));
        var services = new ServiceCollection();
        services.AddMvcCore();
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}

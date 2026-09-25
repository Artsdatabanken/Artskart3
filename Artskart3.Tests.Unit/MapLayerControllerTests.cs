using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Artskart3.Tests.Unit;

public class MapLayerControllerTests
{
    private readonly Mock<IMapLayerService> _serviceMock;
    private readonly MapLayerController _sut;

    public MapLayerControllerTests()
    {
        _serviceMock = new Mock<IMapLayerService>();
        _sut = new MapLayerController(_serviceMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenMapLayerServiceIsNull()
    {
        var act = () => new MapLayerController(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("mapLayerService");
    }

    [Fact]
    public async Task GetMapLayers_ReturnsOkWithMapLayers()
    {
        var mapLayers = new List<MapLayerDto>
        {
            new()
            {
                Id = 1,
                Name = "Grenser",
                Type = "WMS",
                Url = "https://wms.geonorge.no/skwms1/wms.adm_enheter2?",
                Layers = "kommuner_gjel",
                Format = "image/png",
                Version = "1.3.0",
                Attribution = "admGrenserAttribution"
            }
        };
        _serviceMock.Setup(s => s.GetMapLayersAsync(default)).ReturnsAsync(mapLayers);

        var result = await _sut.GetMapLayers();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(mapLayers);
    }

    [Fact]
    public async Task GetMapLayers_WhenServiceReturnsEmpty_ReturnsOkWithEmptyCollection()
    {
        _serviceMock.Setup(s => s.GetMapLayersAsync(default)).ReturnsAsync(Enumerable.Empty<MapLayerDto>());

        var result = await _sut.GetMapLayers();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<MapLayerDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMapLayers_WhenServiceThrowsUnexpectedException_ExceptionPropagates()
    {
        _serviceMock
            .Setup(s => s.GetMapLayersAsync(default))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var act = () => _sut.GetMapLayers();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetMapLayers_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetMapLayersAsync(default)).ReturnsAsync(Enumerable.Empty<MapLayerDto>());

        await _sut.GetMapLayers();

        _serviceMock.Verify(s => s.GetMapLayersAsync(default), Times.Once);
    }
}

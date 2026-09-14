using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class MapLayerServiceTests
{
    private readonly Mock<IMapLayerRepository> _repositoryMock;
    private readonly MapLayerService _sut;

    public MapLayerServiceTests()
    {
        _repositoryMock = new Mock<IMapLayerRepository>();
        _sut = new MapLayerService(_repositoryMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new MapLayerService(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("mapLayerRepository");
    }

    [Fact]
    public async Task GetMapLayersAsync_DelegatesCallToRepository()
    {
        _repositoryMock.Setup(r => r.GetMapLayersAsync(default)).ReturnsAsync(Enumerable.Empty<MapLayerDto>());

        await _sut.GetMapLayersAsync();

        _repositoryMock.Verify(r => r.GetMapLayersAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetMapLayersAsync_ReturnsResultFromRepository()
    {
        var expected = new List<MapLayerDto>
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
        _repositoryMock.Setup(r => r.GetMapLayersAsync(default)).ReturnsAsync(expected);

        var result = await _sut.GetMapLayersAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetMapLayersAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetMapLayersAsync(default)).ReturnsAsync(Enumerable.Empty<MapLayerDto>());

        var result = await _sut.GetMapLayersAsync();

        result.Should().BeEmpty();
    }
}

using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

public class MapLayerRepositoryTests
{
    [Fact]
    public async Task GetMapLayersAsync_ReturnsMapLayers_OrderedByName()
    {
        var context = CreateContext();
        context.Set<MapLayer>().AddRange(
            new MapLayer { Name = "Turkart", Type = "WMTS", Url = "https://example.com/turkart" },
            new MapLayer { Name = "Grenser", Type = "WMS", Url = "https://example.com/grenser" });
        await context.SaveChangesAsync();

        var sut = new MapLayerRepository(context, NullLogger<MapLayerRepository>.Instance);

        var result = (await sut.GetMapLayersAsync()).ToList();

        result.Should().HaveCount(2);
        result.Select(l => l.Name).Should().ContainInOrder("Grenser", "Turkart");
    }

    [Fact]
    public async Task GetMapLayersAsync_ExcludesDeletedMapLayers()
    {
        var context = CreateContext();
        context.Set<MapLayer>().AddRange(
            new MapLayer { Name = "Grenser", Type = "WMS", Url = "https://example.com/grenser" },
            new MapLayer { Name = "Slettet", Type = "WMS", Url = "https://example.com/slettet", IsDeleted = true });
        await context.SaveChangesAsync();

        var sut = new MapLayerRepository(context, NullLogger<MapLayerRepository>.Instance);

        var result = await sut.GetMapLayersAsync();

        result.Should().ContainSingle().Which.Name.Should().Be("Grenser");
    }

    [Fact]
    public async Task GetMapLayersAsync_WhenNoMapLayers_ReturnsEmpty()
    {
        var sut = new MapLayerRepository(CreateContext(), NullLogger<MapLayerRepository>.Instance);

        var result = await sut.GetMapLayersAsync();

        result.Should().BeEmpty();
    }

    private static ArtskartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }
}

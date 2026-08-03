using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchServiceTests
{
    private readonly Mock<ISearchRepository> _repositoryMock;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        _repositoryMock = new Mock<ISearchRepository>();
        _sut = new SearchService(_repositoryMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    // -----------------------------------------------------------------------
    // GetTaxonsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTaxonsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<TaxonDto>
        {
            new() { Id = 1, ValidScientificName = "Parus major" },
            new() { Id = 2, ValidScientificName = "Passer domesticus" }
        };
        _repositoryMock
            .Setup(r => r.GetTaxonsAsync("parus", 20))
            .ReturnsAsync(expected);

        var result = await _sut.GetTaxonsAsync("parus", 20);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetTaxonsAsync_PassesNameAndMaxCountToRepository()
    {
        _repositoryMock
            .Setup(r => r.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Empty<TaxonDto>());

        await _sut.GetTaxonsAsync("canis", 5);

        _repositoryMock.Verify(r => r.GetTaxonsAsync("canis", 5), Times.Once);
    }

    [Fact]
    public async Task GetTaxonsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock
            .Setup(r => r.GetTaxonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Empty<TaxonDto>());

        var result = await _sut.GetTaxonsAsync("nomatch", 20);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // GetLocationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetLocationsAsync_ReturnsListFromRepository()
    {
        var expected = new List<LocationModel>
        {
            new() { Id = 1, Latitude = 59.9, Longitude = 10.7, ObservationCount = 3 }
        };
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetLocationsAsync_WithNullFilter_UsesDefaults()
    {
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocationModel>());

        var result = await _sut.GetLocationsAsync(null);

        result.Should().NotBeNull().And.BeEmpty();
        _repositoryMock.Verify(r => r.GetLocationsAsync(It.IsNotNull<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenRepositoryThrows_PropagatesException()
    {
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var act = () => _sut.GetLocationsAsync(new LocationSearchFilterDto());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB down");
    }

    [Fact]
    public async Task GetLocationsAsync_WithLargeDataSet_100000Items_ReturnsAllItems()
    {
        var largeDataSet = Enumerable.Range(1, 100000)
            .Select(i => new LocationModel
            {
                Id = i,
                Latitude = 59.9 + (i * 0.0001),
                Longitude = 10.7 + (i * 0.0001),
                ObservationCount = i % 10 + 1
            })
            .ToList();

        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(largeDataSet);

        var result = await _sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().HaveCount(100000);
        result.Should().BeEquivalentTo(largeDataSet);
    }
}

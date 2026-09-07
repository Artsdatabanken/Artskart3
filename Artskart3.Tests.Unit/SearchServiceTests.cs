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

    // -----------------------------------------------------------------------
    // GetAreaCountsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreaCountsAsync_WithoutFilters_DerivesCountsFromCachedMarkers()
    {
        var markers = new List<AreaMarkerDto>
        {
            new() { Fid = "03", ObservationCount = 100 },
            new() { Fid = "11", ObservationCount = 200 },
        };
        _repositoryMock
            .Setup(r => r.GetAreaMarkersAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(markers);

        var result = await _sut.GetAreaCountsAsync(1);

        result.Counts.Should().HaveCount(2);
        result.Counts.Should().Contain(c => c.Fid == "03" && c.ObservationCount == 100);
        result.Counts.Should().Contain(c => c.Fid == "11" && c.ObservationCount == 200);
        result.Etag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAreaCountsAsync_WithoutFilters_CachesResultOnSecondCall()
    {
        var markers = new List<AreaMarkerDto>
        {
            new() { Fid = "03", ObservationCount = 100 },
        };
        _repositoryMock
            .Setup(r => r.GetAreaMarkersAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(markers);

        var first = await _sut.GetAreaCountsAsync(1);
        var second = await _sut.GetAreaCountsAsync(1);

        first.Etag.Should().Be(second.Etag);
        // Markør-repository kalles kun én gang (cachet av GetAreaMarkersAsync)
        _repositoryMock.Verify(r => r.GetAreaMarkersAsync(1, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAreaCountsAsync_WithFilters_DelegatesToRepository()
    {
        var filter = new LocationSearchFilterDto { TaxonGroupIds = [1] };
        var counts = new List<AreaCountDto>
        {
            new() { Fid = "03", ObservationCount = 50 },
        };
        _repositoryMock
            .Setup(r => r.GetAreaCountsAsync(1, filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        var result = await _sut.GetAreaCountsAsync(1, filter);

        result.Counts.Should().HaveCount(1);
        result.Counts[0].Fid.Should().Be("03");
        _repositoryMock.Verify(r => r.GetAreaCountsAsync(1, filter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAreaCountsAsync_WithSameFilter_ReturnsCachedResult()
    {
        var filter = new LocationSearchFilterDto { TaxonGroupIds = [1] };
        var counts = new List<AreaCountDto>
        {
            new() { Fid = "03", ObservationCount = 50 },
        };
        _repositoryMock
            .Setup(r => r.GetAreaCountsAsync(1, It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        var first = await _sut.GetAreaCountsAsync(1, filter);
        var second = await _sut.GetAreaCountsAsync(1, filter);

        first.Etag.Should().Be(second.Etag);
        // Repository kalles kun én gang — andre kall er cachet
        _repositoryMock.Verify(r => r.GetAreaCountsAsync(1, It.IsAny<LocationSearchFilterDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

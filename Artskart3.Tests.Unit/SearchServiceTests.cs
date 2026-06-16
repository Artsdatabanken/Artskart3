using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchServiceTests
{
    private readonly Mock<ISearchRepository> _repositoryMock;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        _repositoryMock = new Mock<ISearchRepository>();
        _sut = new SearchService(_repositoryMock.Object);
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
    public async Task GetLocationsAsync_ReturnsGeoJsonString()
    {
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .Returns(AsyncEnumerable(new List<LocationModel>
            {
                new() { Id = 1, East = 262000, North = 6650000, Latitude = 59.9, Longitude = 10.7, ObservationCount = 3 }
            }));

        var result = await _sut.GetLocationsAsync(new LocationSearchFilterDto());

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("FeatureCollection"); // GeoJSON root type
    }

    [Fact]
    public async Task GetLocationsAsync_WithNullFilter_UsesDefaults()
    {
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .Returns(AsyncEnumerable(new List<LocationModel>()));

        var result = await _sut.GetLocationsAsync(null);

        result.Should().NotBeNull();
        _repositoryMock.Verify(r => r.GetLocationsAsync(It.IsNotNull<LocationSearchFilterDto>()), Times.Once);
    }

    [Fact]
    public async Task GetLocationsAsync_WhenRepositoryThrows_WrapsInApplicationException()
    {
        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .Returns(ThrowingAsyncEnumerable<LocationModel>(new InvalidOperationException("DB down")));

        var act = () => _sut.GetLocationsAsync(new LocationSearchFilterDto());

        await act.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task GetLocationsAsync_WithLargeDataSet_100000Items_ReturnsValidGeoJson()
    {
        // Arrange: Create 100,000 location items
        var largeDataSet = Enumerable.Range(1, 100000)
            .Select(i => new LocationModel
            {
                Id = i,
                East = 262000 + i,
                North = 6650000 + i,
                Latitude = 59.9 + (i * 0.0001),
                Longitude = 10.7 + (i * 0.0001),
                ObservationCount = i % 10 + 1,
                Locality = $"Location-{i}"
            })
            .ToList();

        _repositoryMock
            .Setup(r => r.GetLocationsAsync(It.IsAny<LocationSearchFilterDto>()))
            .Returns(AsyncEnumerable(largeDataSet));

        // Act
        var result = await _sut.GetLocationsAsync(new LocationSearchFilterDto());

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("FeatureCollection");
        result.Should().Contain("Feature");
        result.Should().Contain("\"type\":\"Point\"");
        result.Length.Should().BeGreaterThan(100000);
    }

    // -----------------------------------------------------------------------
    // Hjelpemetoder
    // -----------------------------------------------------------------------

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<T> ThrowingAsyncEnumerable<T>(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable CS0162 // Unreachable code detected
        yield break; // unreachable, but required by compiler
#pragma warning restore CS0162 // Unreachable code detected
    }
}

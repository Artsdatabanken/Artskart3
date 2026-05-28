using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchControllerObservationTests
{
    private readonly Mock<ISearchService> _serviceMock = new();
    private readonly Mock<ILogger<SearchController>> _loggerMock = new();

    // -----------------------------------------------------------------------
    // GetObservations – non-paginated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservations_WithNullFilter_ReturnsOk()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(AsyncEnumerable(Array.Empty<ObservationDto>()));

        var result = await sut.GetObservations(null);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetObservations_WithoutPagination_ReturnsOkWithResults()
    {
        var sut = CreateSut();
        var observations = new[]
        {
            new ObservationDto { Id = 1, ScientificName = "Parus major" },
            new ObservationDto { Id = 2, ScientificName = "Passer domesticus" }
        };
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(AsyncEnumerable(observations));

        var result = await sut.GetObservations(new ObservationSearchFilterDto());

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IAsyncEnumerable<ObservationDto>>();
    }

    [Fact]
    public async Task GetObservations_WithoutPagination_CallsServiceOnce()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(AsyncEnumerable(Array.Empty<ObservationDto>()));

        await sut.GetObservations(new ObservationSearchFilterDto());

        _serviceMock.Verify(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // GetObservations – paginated validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public async Task GetObservations_WithPaginationAndInvalidPageNumber_ReturnsBadRequest(int pageNumber)
    {
        var sut = CreateSut();
        var filter = new ObservationSearchFilterDto { PageNumber = pageNumber, ResultsPerPage = 10 };

        var result = await sut.GetObservations(filter);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task GetObservations_WithPaginationAndInvalidResultsPerPage_ReturnsBadRequest(int resultsPerPage)
    {
        var sut = CreateSut();
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = resultsPerPage };

        var result = await sut.GetObservations(filter);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetObservations_WithValidPagination_ReturnsPagedResult()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(new ReusableAsyncEnumerable<ObservationDto>(Array.Empty<ObservationDto>()));

        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 10 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.ResultsPerPage.Should().Be(10);
    }

    [Fact]
    public async Task GetObservations_WithValidPagination_PageNumberAndResultsPerPageMatchFilter()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(new ReusableAsyncEnumerable<ObservationDto>(Array.Empty<ObservationDto>()));

        var filter = new ObservationSearchFilterDto { PageNumber = 3, ResultsPerPage = 25 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.PageNumber.Should().Be(3);
        pagedResult.ResultsPerPage.Should().Be(25);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private SearchController CreateSut() => new(_serviceMock.Object, _loggerMock.Object);

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Supports multiple enumerations — required when the controller calls both
    /// CountAsync() and Take() on the same IAsyncEnumerable instance.
    /// </summary>
    private sealed class ReusableAsyncEnumerable<T>(IEnumerable<T> items) : IAsyncEnumerable<T>
    {
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }
}

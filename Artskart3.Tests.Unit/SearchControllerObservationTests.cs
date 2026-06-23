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
    private readonly Mock<ISpeciesService> _speciesServiceMock = new();
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
            .ReturnsAsync(new List<ObservationDto>());

        var result = await sut.GetObservations(null);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetObservations_WithoutPagination_ReturnsOkWithResults()
    {
        var sut = CreateSut();
        var observations = new List<ObservationDto>
        {
            new ObservationDto { Id = 1, ScientificName = "Parus major" },
            new ObservationDto { Id = 2, ScientificName = "Passer domesticus" }
        };
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(observations);

        var result = await sut.GetObservations(new ObservationSearchFilterDto());

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<ObservationDto>>();
    }

    [Fact]
    public async Task GetObservations_WithoutPagination_CallsServiceOnce()
    {
        var sut = CreateSut();
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(new List<ObservationDto>());

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
            .ReturnsAsync(new List<ObservationDto>());

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
            .ReturnsAsync(new List<ObservationDto>());

        var filter = new ObservationSearchFilterDto { PageNumber = 3, ResultsPerPage = 25 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.PageNumber.Should().Be(3);
        pagedResult.ResultsPerPage.Should().Be(25);
    }

    // -----------------------------------------------------------------------
    // GetObservations – paginated data correctness
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservations_WithPagination_Page1ReturnsFirstItems()
    {
        var sut = CreateSut();
        // Simulerer at repositoriet returnerer et lookahead-vindu fra rad 1 (side 1, allerede offset i SQL)
        var lookaheadWindow = CreateObservationsFromOffset(1, 40);
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(lookaheadWindow);

        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 10 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.Items.Should().HaveCount(10);
        pagedResult.Items.First().Id.Should().Be(1);
        pagedResult.Items.Last().Id.Should().Be(10);
    }

    [Fact]
    public async Task GetObservations_WithPagination_Page2ReturnsNextItems()
    {
        var sut = CreateSut();
        // Simulerer at repositoriet allerede har gjort Skip(10) i SQL og returnerer fra rad 11
        var lookaheadWindow = CreateObservationsFromOffset(11, 40);
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(lookaheadWindow);

        var filter = new ObservationSearchFilterDto { PageNumber = 2, ResultsPerPage = 10 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.Items.Should().HaveCount(10);
        pagedResult.Items.First().Id.Should().Be(11);
        pagedResult.Items.Last().Id.Should().Be(20);
    }

    [Fact]
    public async Task GetObservations_WithPagination_LastPageReturnsRemainingItems()
    {
        var sut = CreateSut();
        // Simulerer at repositoriet returnerer de siste 5 radene (rad 21-25) for side 3
        var lookaheadWindow = CreateObservationsFromOffset(21, 5);
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(lookaheadWindow);

        var filter = new ObservationSearchFilterDto { PageNumber = 3, ResultsPerPage = 10 };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.Items.Should().HaveCount(5);
        pagedResult.Items.First().Id.Should().Be(21);
    }

    [Theory]
    [InlineData(10, 10, 0)]  // 10 items, 10/page → 1 side, lookahead = 0
    [InlineData(25, 10, 2)]  // 25 items, 10/page → 3 sider, lookahead = 2
    [InlineData(20, 10, 1)]  // 20 items, 10/page → 2 sider, lookahead = 1
    [InlineData(1, 10, 0)]   // 1 item, 10/page → 1 side, lookahead = 0
    public async Task GetObservations_WithPagination_CalculatesCorrectLookaheadCount(
        int totalItems, int resultsPerPage, int expectedLookahead)
    {
        var sut = CreateSut();
        var allItems = CreateObservations(totalItems);
        _serviceMock
            .Setup(s => s.GetObservationsAsync(It.IsAny<ObservationSearchFilterDto>()))
            .ReturnsAsync(allItems);

        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = resultsPerPage };
        var result = await sut.GetObservations(filter);

        var pagedResult = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<PagedObservationResponseDto>().Subject;
        pagedResult.LookaheadCount.Should().Be(expectedLookahead);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private SearchController CreateSut() => new(_serviceMock.Object, _speciesServiceMock.Object, _loggerMock.Object);

    private static List<ObservationDto> CreateObservations(int count) =>
        CreateObservationsFromOffset(1, count);

    private static List<ObservationDto> CreateObservationsFromOffset(int startId, int count) =>
        Enumerable.Range(startId, count)
            .Select(i => new ObservationDto { Id = i, ScientificName = $"Species {i}" })
            .ToList();
}

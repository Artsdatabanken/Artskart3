using Artskart3.Api.Controllers;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

public class SearchControllerPlaceSearchTests
{
    private readonly Mock<ISearchService> _searchServiceMock = new();
    private readonly Mock<ISpeciesService> _speciesServiceMock = new();
    private readonly Mock<IPlaceSearchService> _placeSearchServiceMock = new();
    private readonly Mock<ILogger<SearchController>> _loggerMock = new();

    private SearchController CreateSut() => new(_searchServiceMock.Object, _speciesServiceMock.Object, _placeSearchServiceMock.Object, _loggerMock.Object, Options.Create(new PaginationOptions()));

    // -----------------------------------------------------------------------
    // Validering
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchPlaces_WithEmptyOrNullSearch_ReturnsBadRequest(string? search)
    {
        var sut = CreateSut();

        var result = await sut.SearchPlaces(search!);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // -----------------------------------------------------------------------
    // Vellykket søk
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchPlaces_WithValidSearch_ReturnsOkWithResults()
    {
        var sut = CreateSut();
        var expected = new List<PlaceSearchResultDto>
        {
            new() { StedsNummer = 307915, Name = "Oslo", NavneObjektType = "By", East = 261000, North = 6649000, CoordinateSystem = 25833 }
        };
        _placeSearchServiceMock
            .Setup(s => s.SearchPlacesAsync("Oslo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await sut.SearchPlaces("Oslo");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SearchPlaces_WhenServiceReturnsEmpty_ReturnsOkWithEmptyList()
    {
        var sut = CreateSut();
        _placeSearchServiceMock
            .Setup(s => s.SearchPlacesAsync("finnesikke", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await sut.SearchPlaces("finnesikke");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<PlaceSearchResultDto>>()
            .Which.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – Geonorge utilgjengelig (502)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchPlaces_WhenHttpRequestExceptionThrown_Returns502()
    {
        var sut = CreateSut();
        _placeSearchServiceMock
            .Setup(s => s.SearchPlacesAsync("Oslo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await sut.SearchPlaces("Oslo");

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(502);
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – Geonorge timeout (504)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchPlaces_WhenTimeoutExceptionThrown_Returns504()
    {
        var sut = CreateSut();
        _placeSearchServiceMock
            .Setup(s => s.SearchPlacesAsync("Oslo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await sut.SearchPlaces("Oslo");

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(504);
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – uventet feil kastes videre
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchPlaces_WhenUnexpectedExceptionThrown_Rethrows()
    {
        var sut = CreateSut();
        _placeSearchServiceMock
            .Setup(s => s.SearchPlacesAsync("Oslo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var act = () => sut.SearchPlaces("Oslo");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

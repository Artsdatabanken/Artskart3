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

public class SearchControllerSpeciesTests
{
    private readonly Mock<ISearchService> _searchServiceMock = new();
    private readonly Mock<ISpeciesService> _speciesServiceMock = new();
    private readonly Mock<ILogger<SearchController>> _loggerMock = new();

    private SearchController CreateSut() => new(_searchServiceMock.Object, _speciesServiceMock.Object, _loggerMock.Object, Options.Create(new PaginationOptions()));

    // -----------------------------------------------------------------------
    // Validering
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchSpecies_WithEmptyOrNullSearch_ReturnsBadRequest(string? search)
    {
        var sut = CreateSut();

        var result = await sut.SearchSpecies(search!);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // -----------------------------------------------------------------------
    // Vellykket søk
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpecies_WithValidSearch_ReturnsOkWithResults()
    {
        var sut = CreateSut();
        var expected = new List<SpeciesDto>
        {
            new() { TaxonId = 79773, ScientificName = "Margaritifera margaritifera", Rank = "Species" }
        };
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("elvemusling", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await sut.SearchSpecies("elvemusling");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SearchSpecies_WithIntegerSearch_ReturnsOkWithResults()
    {
        var sut = CreateSut();
        var expected = new List<SpeciesDto>
        {
            new() { TaxonId = 79773, ScientificName = "Margaritifera margaritifera" }
        };
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("79773", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await sut.SearchSpecies("79773");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task SearchSpecies_WhenServiceReturnsEmpty_ReturnsOkWithEmptyList()
    {
        var sut = CreateSut();
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("finnesikke", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await sut.SearchSpecies("finnesikke");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<SpeciesDto>>()
            .Which.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – NorTaxa utilgjengelig (502)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpecies_WhenHttpRequestExceptionThrown_Returns502()
    {
        var sut = CreateSut();
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("elvemusling", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await sut.SearchSpecies("elvemusling");

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(502);
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – NorTaxa timeout (504)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpecies_WhenTimeoutExceptionThrown_Returns504()
    {
        var sut = CreateSut();
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("elvemusling", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await sut.SearchSpecies("elvemusling");

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(504);
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering – uventet feil kastes videre
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpecies_WhenUnexpectedExceptionThrown_Rethrows()
    {
        var sut = CreateSut();
        _speciesServiceMock
            .Setup(s => s.SearchSpeciesAsync("elvemusling", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var act = () => sut.SearchSpecies("elvemusling");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

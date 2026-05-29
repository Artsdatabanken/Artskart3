using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class LookupControllerTests
{
    private readonly Mock<ILookupService> _serviceMock;
    private readonly Mock<ILogger<LookupController>> _loggerMock;
    private readonly LookupController _sut;

    public LookupControllerTests()
    {
        _serviceMock = new Mock<ILookupService>();
        _loggerMock = new Mock<ILogger<LookupController>>();
        _sut = new LookupController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLookupServiceIsNull()
    {
        var act = () => new LookupController(null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("lookupService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new LookupController(_serviceMock.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task GetCategories_ReturnsOkWithCategories()
    {
        var categories = new List<CategoryTypeDto>
        {
            new()
            {
                Id = 1,
                Name = "Rødliste",
                Categories = [new CategoryDto { Id = 10, Code = "CR", Name = "Critically Endangered" }]
            }
        };
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(categories);

        var result = await _sut.GetCategories();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(categories);
    }

    [Fact]
    public async Task GetCategories_WhenServiceReturnsEmpty_ReturnsOkWithEmptyCollection()
    {
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(Enumerable.Empty<CategoryTypeDto>());

        var result = await _sut.GetCategories();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<CategoryTypeDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategories_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetCategoriesAsync())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await _sut.GetCategories();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetCategories_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(Enumerable.Empty<CategoryTypeDto>());

        await _sut.GetCategories();

        _serviceMock.Verify(s => s.GetCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAreas_ReturnsOkWithAreas()
    {
        var areas = new List<AreaTypeDto>
        {
            new()
            {
                Id = 1,
                Name = "Fylke",
                Areas = [new AreaDto { Id = 10, Fid = "03", Name = "Oslo", IsCurrent = true, ObservationCount = 500 }]
            }
        };
        _serviceMock.Setup(s => s.GetAreasAsync()).ReturnsAsync(areas);

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(areas);
    }

    [Fact]
    public async Task GetAreas_WhenServiceReturnsEmpty_ReturnsOkWithEmptyCollection()
    {
        _serviceMock.Setup(s => s.GetAreasAsync()).ReturnsAsync(Enumerable.Empty<AreaTypeDto>());

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<AreaTypeDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAreas_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetAreasAsync())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await _sut.GetAreas();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetAreas_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetAreasAsync()).ReturnsAsync(Enumerable.Empty<AreaTypeDto>());

        await _sut.GetAreas();

        _serviceMock.Verify(s => s.GetAreasAsync(), Times.Once);
    }

    [Fact]
    public async Task GetInstitutions_ReturnsOkWithInstitutions()
    {
        var institutions = new List<InstitutionDto>
        {
            new() { Id = 1, Name = "Naturhistorisk museum", Code = "NHM", ObservationCount = 1000 }
        };
        _serviceMock.Setup(s => s.GetInstitutionsAsync()).ReturnsAsync(institutions);

        var result = await _sut.GetInstitutions();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(institutions);
    }

    [Fact]
    public async Task GetInstitutions_WhenServiceReturnsEmpty_ReturnsOkWithEmptyCollection()
    {
        _serviceMock.Setup(s => s.GetInstitutionsAsync()).ReturnsAsync(Enumerable.Empty<InstitutionDto>());

        var result = await _sut.GetInstitutions();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<InstitutionDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstitutions_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetInstitutionsAsync())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await _sut.GetInstitutions();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetInstitutions_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetInstitutionsAsync()).ReturnsAsync(Enumerable.Empty<InstitutionDto>());

        await _sut.GetInstitutions();

        _serviceMock.Verify(s => s.GetInstitutionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetTaxonGroups_ReturnsOkWithTaxonGroups()
    {
        var taxonGroups = new List<TaxonGroupDto>
        {
            new() { Id = 1, Name = "Fugler", ObservationCount = 2000 }
        };
        _serviceMock.Setup(s => s.GetTaxonGroupsAsync()).ReturnsAsync(taxonGroups);

        var result = await _sut.GetTaxonGroups();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(taxonGroups);
    }

    [Fact]
    public async Task GetTaxonGroups_WhenServiceReturnsEmpty_ReturnsOkWithEmptyCollection()
    {
        _serviceMock.Setup(s => s.GetTaxonGroupsAsync()).ReturnsAsync(Enumerable.Empty<TaxonGroupDto>());

        var result = await _sut.GetTaxonGroups();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<TaxonGroupDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxonGroups_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetTaxonGroupsAsync())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await _sut.GetTaxonGroups();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetTaxonGroups_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetTaxonGroupsAsync()).ReturnsAsync(Enumerable.Empty<TaxonGroupDto>());

        await _sut.GetTaxonGroups();

        _serviceMock.Verify(s => s.GetTaxonGroupsAsync(), Times.Once);
    }
}

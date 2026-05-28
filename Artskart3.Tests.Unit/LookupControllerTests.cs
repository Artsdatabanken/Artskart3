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
    public async Task GetCategories_ReturnsOkWithCategoryListDto()
    {
        var categoryList = new CategoryListDto
        {
            CategoryTypes =
            [
                new CategoryTypeDto
                {
                    Id = 1,
                    Name = "Rødliste",
                    Categories = [new CategoryDto { Id = 10, Code = "CR", Name = "Critically Endangered" }]
                }
            ]
        };
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(categoryList);

        var result = await _sut.GetCategories();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(categoryList);
    }

    [Fact]
    public async Task GetCategories_WhenServiceReturnsEmptyCategoryTypes_ReturnsOkWithEmptyCategoryListDto()
    {
        var emptyList = new CategoryListDto { CategoryTypes = [] };
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(emptyList);

        var result = await _sut.GetCategories();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CategoryListDto>()
            .Which.CategoryTypes.Should().BeEmpty();
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
        _serviceMock.Setup(s => s.GetCategoriesAsync()).ReturnsAsync(new CategoryListDto());

        await _sut.GetCategories();

        _serviceMock.Verify(s => s.GetCategoriesAsync(), Times.Once);
    }
}

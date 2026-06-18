using Artskart3.Api.Controllers;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class WarningsControllerTests
{
    private readonly Mock<IWarningsService> _serviceMock;
    private readonly Mock<ILogger<WarningsController>> _loggerMock;
    private readonly WarningsController _sut;

    public WarningsControllerTests()
    {
        _serviceMock = new Mock<IWarningsService>();
        _loggerMock = new Mock<ILogger<WarningsController>>();
        _sut = new WarningsController(_serviceMock.Object, _loggerMock.Object);
    }

    // -----------------------------------------------------------------------
    // GetWarnings
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetWarnings_ReturnsOkWithWarnings()
    {
        var warnings = new List<WarningModel>
        {
            new() { Type = AlertType.Danger, Heading = "Tjenesten er nede", CanClose = false },
            new() { Type = AlertType.Info,   Heading = "Ny versjon",        CanClose = true  }
        };
        _serviceMock.Setup(s => s.GetAllWarningsAsync()).ReturnsAsync(warnings);

        var result = await _sut.GetWarnings();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(warnings);
    }

    [Fact]
    public async Task GetWarnings_WhenServiceReturnsEmptyList_ReturnsOkWithEmptyList()
    {
        _serviceMock.Setup(s => s.GetAllWarningsAsync()).ReturnsAsync([]);

        var result = await _sut.GetWarnings();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<WarningModel>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWarnings_WhenServiceThrowsApplicationException_Returns503()
    {
        _serviceMock
            .Setup(s => s.GetAllWarningsAsync())
            .ThrowsAsync(new ApplicationException("Fil ikke tilgjengelig"));

        var result = await _sut.GetWarnings();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetWarnings_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetAllWarningsAsync())
            .ThrowsAsync(new InvalidOperationException("Uventet feil"));

        var result = await _sut.GetWarnings();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }
}

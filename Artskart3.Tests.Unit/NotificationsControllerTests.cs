using Artskart3.Api.Controllers;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationsService> _serviceMock;
    private readonly Mock<ILogger<NotificationsController>> _loggerMock;
    private readonly NotificationsController _sut;

    public NotificationsControllerTests()
    {
        _serviceMock = new Mock<INotificationsService>();
        _loggerMock = new Mock<ILogger<NotificationsController>>();
        _sut = new NotificationsController(_serviceMock.Object, _loggerMock.Object);
    }

    // -----------------------------------------------------------------------
    // GetNotifications
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetNotifications_ReturnsOkWithNotifications()
    {
        var notifications = new List<NotificationModel>
        {
            new() { Type = AlertType.Danger, Heading = "Tjenesten er nede", CanClose = false },
            new() { Type = AlertType.Info,   Heading = "Ny versjon",        CanClose = true  }
        };
        _serviceMock.Setup(s => s.GetAllNotificationsAsync()).ReturnsAsync(notifications);

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(notifications);
    }

    [Fact]
    public async Task GetNotifications_WhenServiceReturnsEmptyList_ReturnsOkWithEmptyList()
    {
        _serviceMock.Setup(s => s.GetAllNotificationsAsync()).ReturnsAsync([]);

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<NotificationModel>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotifications_WhenServiceThrowsApplicationException_Returns503()
    {
        _serviceMock
            .Setup(s => s.GetAllNotificationsAsync())
            .ThrowsAsync(new ApplicationException("Fil ikke tilgjengelig"));

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task GetNotifications_WhenServiceThrowsUnexpectedException_Returns500()
    {
        _serviceMock
            .Setup(s => s.GetAllNotificationsAsync())
            .ThrowsAsync(new InvalidOperationException("Uventet feil"));

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }
}

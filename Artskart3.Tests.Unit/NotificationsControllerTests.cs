using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
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
        _sut = new NotificationsController(_serviceMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenServiceIsNull()
    {
        var act = () => new NotificationsController(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("notificationsService");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var act = () => new NotificationsController(_serviceMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task GetNotifications_ReturnsOkWithNotifications()
    {
        var notifications = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), AlertType = AlertType.Danger, Heading = "Tjenesten er nede", Description = "Prøv igjen senere", CanClose = false },
            new() { Id = Guid.NewGuid(), AlertType = AlertType.Info, Heading = "Ny versjon", Description = "Se hva som er nytt", CanClose = true }
        };
        _serviceMock.Setup(s => s.GetAllActiveNotificationsAsync()).ReturnsAsync(notifications);

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(notifications);
    }

    [Fact]
    public async Task GetNotifications_WhenServiceReturnsEmptyList_ReturnsOkWithEmptyList()
    {
        _serviceMock.Setup(s => s.GetAllActiveNotificationsAsync()).ReturnsAsync([]);

        var result = await _sut.GetNotifications();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<NotificationDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotifications_CallsServiceOnce()
    {
        _serviceMock.Setup(s => s.GetAllActiveNotificationsAsync()).ReturnsAsync([]);

        await _sut.GetNotifications();

        _serviceMock.Verify(s => s.GetAllActiveNotificationsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetNotifications_WhenServiceThrowsApplicationException_ExceptionPropagates()
    {
        _serviceMock
            .Setup(s => s.GetAllActiveNotificationsAsync())
            .ThrowsAsync(new ApplicationException("Databasen er utilgjengelig"));

        var act = () => _sut.GetNotifications();

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("Databasen er utilgjengelig");
    }

    [Fact]
    public async Task GetNotifications_WhenServiceThrowsUnexpectedException_ExceptionPropagates()
    {
        _serviceMock
            .Setup(s => s.GetAllActiveNotificationsAsync())
            .ThrowsAsync(new InvalidOperationException("Uventet feil"));

        var act = () => _sut.GetNotifications();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

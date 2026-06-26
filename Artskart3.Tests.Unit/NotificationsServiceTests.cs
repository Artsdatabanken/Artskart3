using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class NotificationsServiceTests
{
    private readonly Mock<INotificationsRepository> _repositoryMock;
    private readonly NotificationsService _sut;

    public NotificationsServiceTests()
    {
        _repositoryMock = new Mock<INotificationsRepository>();
        _sut = new NotificationsService(_repositoryMock.Object);
    }

    // -----------------------------------------------------------------------
    // GetAllNotificationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAllNotificationsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<NotificationModel>
        {
            new() { Type = AlertType.Warning, Heading = "Planlagt vedlikehold", CanClose = true },
            new() { Type = AlertType.Info,    Heading = "Ny versjon",           CanClose = true }
        };
        _repositoryMock.Setup(r => r.GetAllNotificationsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetAllNotificationsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAllNotificationsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetAllNotificationsAsync()).ReturnsAsync([]);

        var result = await _sut.GetAllNotificationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllNotificationsAsync_WhenRepositoryThrowsApplicationException_RethrowsIt()
    {
        var original = new ApplicationException("Fil ikke tilgjengelig");
        _repositoryMock.Setup(r => r.GetAllNotificationsAsync()).ThrowsAsync(original);

        var act = () => _sut.GetAllNotificationsAsync();

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("Fil ikke tilgjengelig");
    }

    [Fact]
    public async Task GetAllNotificationsAsync_WhenRepositoryThrowsUnexpectedException_WrapsInApplicationException()
    {
        _repositoryMock
            .Setup(r => r.GetAllNotificationsAsync())
            .ThrowsAsync(new InvalidOperationException("Uventet feil"));

        var act = () => _sut.GetAllNotificationsAsync();

        await act.Should().ThrowAsync<ApplicationException>()
            .Where(e => e.InnerException is InvalidOperationException);
    }
}

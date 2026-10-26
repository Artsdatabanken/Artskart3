using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.Entities;
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

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new NotificationsService(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("notificationsRepository");
    }

    [Fact]
    public async Task GetAllNotificationsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<Notification>
        {
            new() { Id = Guid.NewGuid(), AlertType = AlertType.Warning, Heading = "Planlagt vedlikehold", Description = "Fredag kl. 22", CanClose = true },
            new() { Id = Guid.NewGuid(), AlertType = AlertType.Info, Heading = "Ny versjon", Description = "Se hva som er nytt", CanClose = true }
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
        var original = new ApplicationException("Databasen er utilgjengelig");
        _repositoryMock.Setup(r => r.GetAllNotificationsAsync()).ThrowsAsync(original);

        var act = () => _sut.GetAllNotificationsAsync();

        (await act.Should().ThrowAsync<ApplicationException>())
            .Which.Should().BeSameAs(original);
    }

    [Fact]
    public async Task GetAllNotificationsAsync_WhenRepositoryThrowsUnexpectedException_WrapsInApplicationException()
    {
        var inner = new InvalidOperationException("Uventet feil");
        _repositoryMock
            .Setup(r => r.GetAllNotificationsAsync())
            .ThrowsAsync(inner);

        var act = () => _sut.GetAllNotificationsAsync();

        (await act.Should().ThrowAsync<ApplicationException>())
            .Which.InnerException.Should().BeSameAs(inner);
    }
}

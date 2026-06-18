using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class WarningsServiceTests
{
    private readonly Mock<IWarningsRepository> _repositoryMock;
    private readonly WarningsService _sut;

    public WarningsServiceTests()
    {
        _repositoryMock = new Mock<IWarningsRepository>();
        _sut = new WarningsService(_repositoryMock.Object);
    }

    // -----------------------------------------------------------------------
    // GetAllWarningsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAllWarningsAsync_ReturnsResultFromRepository()
    {
        var expected = new List<WarningModel>
        {
            new() { Type = AlertType.Warning, Heading = "Planlagt vedlikehold", CanClose = true },
            new() { Type = AlertType.Info,    Heading = "Ny versjon",           CanClose = true }
        };
        _repositoryMock.Setup(r => r.GetAllWarningsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetAllWarningsAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAllWarningsAsync_WhenRepositoryReturnsEmpty_ReturnsEmpty()
    {
        _repositoryMock.Setup(r => r.GetAllWarningsAsync()).ReturnsAsync([]);

        var result = await _sut.GetAllWarningsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllWarningsAsync_WhenRepositoryThrowsApplicationException_RethrowsIt()
    {
        var original = new ApplicationException("Fil ikke tilgjengelig");
        _repositoryMock.Setup(r => r.GetAllWarningsAsync()).ThrowsAsync(original);

        var act = () => _sut.GetAllWarningsAsync();

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("Fil ikke tilgjengelig");
    }

    [Fact]
    public async Task GetAllWarningsAsync_WhenRepositoryThrowsUnexpectedException_WrapsInApplicationException()
    {
        _repositoryMock
            .Setup(r => r.GetAllWarningsAsync())
            .ThrowsAsync(new InvalidOperationException("Uventet feil"));

        var act = () => _sut.GetAllWarningsAsync();

        await act.Should().ThrowAsync<ApplicationException>()
            .Where(e => e.InnerException is InvalidOperationException);
    }
}

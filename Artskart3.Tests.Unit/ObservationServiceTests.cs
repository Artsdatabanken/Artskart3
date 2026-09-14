using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class ObservationServiceTests
{
    private readonly Mock<IObservationRepository> _repositoryMock = new();
    private readonly ObservationService _sut;

    public ObservationServiceTests()
    {
        _sut = new ObservationService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetObservationDetails_ReturnsResultFromRepository()
    {
        var expected = new ObservationDto { Id = 42, ScientificName = "Parus major" };
        _repositoryMock
            .Setup(r => r.GetObservationDetails(10, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetObservationDetails(10, 42);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetObservationDetails_ForwardsAllArguments()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        _repositoryMock
            .Setup(r => r.GetObservationDetails(10, 42, cancellationToken))
            .ReturnsAsync(new ObservationDto { Id = 42 });

        await _sut.GetObservationDetails(10, 42, cancellationToken);

        _repositoryMock.Verify(r => r.GetObservationDetails(10, 42, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetObservationsByLocations_ReturnsResultFromRepository()
    {
        var expected = new List<ObservationListInfoDto> { new() { Id = 42, LocationId = 10 } };
        _repositoryMock
            .Setup(r => r.GetObservationByLocations(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetObservationsByLocations([10]);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetObservationsByLocations_ForwardsAllArguments()
    {
        var locationIds = new[] { 10, 20 };
        var cancellationToken = new CancellationTokenSource().Token;
        _repositoryMock
            .Setup(r => r.GetObservationByLocations(locationIds, cancellationToken))
            .ReturnsAsync(Enumerable.Empty<ObservationListInfoDto>());

        await _sut.GetObservationsByLocations(locationIds, cancellationToken);

        _repositoryMock.Verify(r => r.GetObservationByLocations(locationIds, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetObservationDetails_WhenRepositoryThrows_PropagatesException()
    {
        _repositoryMock
            .Setup(r => r.GetObservationDetails(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("repository failure"));

        var act = () => _sut.GetObservationDetails(10, 42);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("repository failure");
    }
}

using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class ObservationControllerTests
{
    private readonly Mock<IObservationService> _serviceMock = new();
    private readonly Mock<ILogger<ObservationController>> _loggerMock = new();

    [Fact]
    public async Task GetObservationDetails_ReturnsObservationFromService()
    {
        var expected = new ObservationDto { Id = 42, ScientificName = "Parus major" };
        _serviceMock
            .Setup(s => s.GetObservationDetails(10, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var sut = CreateSut();

        var result = await sut.GetObservationDetails(10, 42);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetObservationDetails_ForwardsCancellationToken()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        _serviceMock
            .Setup(s => s.GetObservationDetails(10, 42, cancellationToken))
            .ReturnsAsync(new ObservationDto { Id = 42 });
        var sut = CreateSut();

        await sut.GetObservationDetails(10, 42, cancellationToken);

        _serviceMock.Verify(s => s.GetObservationDetails(10, 42, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetObservationsByLocations_ReturnsObservationsFromService()
    {
        var expected = new List<ObservationListInfoDto> { new() { Id = 42, LocationId = 10 } };
        _serviceMock
            .Setup(s => s.GetObservationsByLocations(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var sut = CreateSut();

        var result = await sut.GetObservationsByLocations([10]);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetObservationsByLocations_ForwardsLocationIdsAndCancellationToken()
    {
        var locationIds = new[] { 10, 20 };
        var cancellationToken = new CancellationTokenSource().Token;
        _serviceMock
            .Setup(s => s.GetObservationsByLocations(locationIds, cancellationToken))
            .ReturnsAsync(Enumerable.Empty<ObservationListInfoDto>());
        var sut = CreateSut();

        await sut.GetObservationsByLocations(locationIds, cancellationToken);

        _serviceMock.Verify(s => s.GetObservationsByLocations(locationIds, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetObservationDetails_WhenServiceThrows_PropagatesException()
    {
        _serviceMock
            .Setup(s => s.GetObservationDetails(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("service failure"));
        var sut = CreateSut();

        var act = () => sut.GetObservationDetails(10, 42);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("service failure");
    }

    private ObservationController CreateSut() => new(_serviceMock.Object, _loggerMock.Object);
}

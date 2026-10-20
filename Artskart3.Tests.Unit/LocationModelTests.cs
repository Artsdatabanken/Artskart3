using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class LocationModelTests
{
    [Fact]
    public void ObservationCount_CanBeSetAndRetrieved()
    {
        var sut = new LocationModel { ObservationCount = 7 };

        sut.ObservationCount.Should().Be(7);
    }

    [Fact]
    public void DefaultValues_AreZeroAndFound()
    {
        var sut = new LocationModel();

        sut.Id.Should().Be(0);
        sut.Latitude.Should().Be(0);
        sut.Longitude.Should().Be(0);
        sut.ObservationCount.Should().Be(0);
        sut.RegistrationStatus.Should().Be(RegistrationStatus.Found);
    }
}

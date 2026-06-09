using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class LocationModelTests
{
    [Fact]
    public void ObservationCount_ReturnsBackingField_WhenObservationsIsEmpty()
    {
        var sut = new LocationModel { ObservationCount = 7 };

        sut.ObservationCount.Should().Be(7);
    }

    [Fact]
    public void ObservationCount_UsesObservationsCount_WhenObservationsArePopulated()
    {
        var sut = new LocationModel
        {
            ObservationCount = 1,
            Observations =
            {
                new ObservationModel(),
                new ObservationModel(),
                new ObservationModel()
            }
        };

        sut.ObservationCount.Should().Be(3);
    }

    [Fact]
    public void ObservationCount_SetterWorksIndependently_WhenObservationsAreCleared()
    {
        var sut = new LocationModel
        {
            Observations =
            {
                new ObservationModel(),
                new ObservationModel()
            }
        };

        _ = sut.ObservationCount;
        sut.Observations.Clear();
        sut.ObservationCount = 9;

        sut.ObservationCount.Should().Be(9);
    }

    [Fact]
    public void MaxCategory_ReturnsUnknown_WhenObservationsIsEmpty()
    {
        var sut = new LocationModel();

        sut.MaxCategory.Should().Be(Category.Unknown);
    }

    [Fact]
    public void MaxCategory_ReturnsHighestCategory_WhenObservationsContainObservationModels()
    {
        var sut = new LocationModel
        {
            Observations =
            {
                new ObservationModel { Category = Category.NT },
                new ObservationWithLocationModel { Category = Category.CR }
            }
        };

        sut.MaxCategory.Should().Be(Category.CR);
    }

    [Fact]
    public void MaxCategory_ThrowsException_WhenObservationHasUnexpectedType()
    {
        var sut = new LocationModel
        {
            Observations = { new FakeObservation() }
        };

        var act = () => sut.MaxCategory;

        act.Should().Throw<Exception>()
            .WithMessage("Cast of observation in Location.MaxCategory failed");
    }

    private sealed class FakeObservation : ObservationBaseModel
    {
    }
}
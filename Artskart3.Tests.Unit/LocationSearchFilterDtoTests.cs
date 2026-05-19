using Artskart3.Core.Application.DTOs;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class LocationSearchFilterDtoTests
{
    [Fact]
    public void MaxResults_HarStandardverdi1000()
    {
        var sut = new LocationSearchFilterDto();

        sut.MaxResults.Should().Be(1000);
    }

    [Fact]
    public void CoordinatePrecisionFrom_HarStandardverdi0()
    {
        var sut = new LocationSearchFilterDto();

        sut.CoordinatePrecisionFrom.Should().Be(0);
    }

    [Fact]
    public void CoordinatePrecisionTo_HarStandardverdi0()
    {
        var sut = new LocationSearchFilterDto();

        sut.CoordinatePrecisionTo.Should().Be(0);
    }

    [Fact]
    public void Epsg_ErNullSomStandard()
    {
        var sut = new LocationSearchFilterDto();

        sut.Epsg.Should().BeNull();
    }

    [Fact]
    public void AlleEgenskaper_KanSettes()
    {
        var sut = new LocationSearchFilterDto
        {
            TaxonGroupIds = "1,2",
            Categories = "3,4",
            BasisOfRecords = "5,6",
            CollectionIds = "NHM,GBIF",
            CoordinatePrecisionFrom = 10,
            CoordinatePrecisionTo = 100,
            Epsg = 25833,
            MaxResults = 250
        };

        sut.TaxonGroupIds.Should().Be("1,2");
        sut.Categories.Should().Be("3,4");
        sut.BasisOfRecords.Should().Be("5,6");
        sut.CollectionIds.Should().Be("NHM,GBIF");
        sut.CoordinatePrecisionFrom.Should().Be(10);
        sut.CoordinatePrecisionTo.Should().Be(100);
        sut.Epsg.Should().Be(25833);
        sut.MaxResults.Should().Be(250);
    }
}
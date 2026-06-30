using Artskart3.Core.Application.DTOs;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class LocationSearchFilterDtoTests
{
    [Fact]
    public void MaxResults_HarStandardverdi1000()
    {
        var sut = new LocationSearchFilterDto();

        sut.MaxResults.Should().Be(100000);
    }

    [Fact]
    public void CoordinatePrecision_ErNullSomStandard()
    {
        var sut = new LocationSearchFilterDto();

        sut.CoordinatePrecision.Should().BeNull();
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
            TaxonGroupIds = new[] { 1, 2 },
            CategoryIds = new[] { 3, 4 },
            BasisOfRecordIds = new[] { 5, 6 },
            OrganizationIds = new[] { 7, 8 },
            MunicipalityIds = new[] { "0301", "1103" },
            CountyIds = new[] { "03", "11" },
            OceanAreaIds = new[] { "500" },
            BehaviorIds = new[] { 9, 10 },
            CoordinatePrecision = new CoordinatePrecisionDto { From = 10, To = 100 },
            Period = new PeriodDto { From = 2000, To = 2024 },
            Epsg = 25833,
            MaxResults = 250
        };

        sut.TaxonGroupIds.Should().BeEquivalentTo(new[] { 1, 2 });
        sut.CategoryIds.Should().BeEquivalentTo(new[] { 3, 4 });
        sut.BasisOfRecordIds.Should().BeEquivalentTo(new[] { 5, 6 });
        sut.OrganizationIds.Should().BeEquivalentTo(new[] { 7, 8 });
        sut.MunicipalityIds.Should().BeEquivalentTo(new[] { "0301", "1103" });
        sut.CountyIds.Should().BeEquivalentTo(new[] { "03", "11" });
        sut.OceanAreaIds.Should().BeEquivalentTo(new[] { "500" });
        sut.BehaviorIds.Should().BeEquivalentTo(new[] { 9, 10 });
        sut.CoordinatePrecision!.From.Should().Be(10);
        sut.CoordinatePrecision!.To.Should().Be(100);
        sut.Period!.From.Should().Be(2000);
        sut.Period!.To.Should().Be(2024);
        sut.Epsg.Should().Be(25833);
        sut.MaxResults.Should().Be(250);
    }
}

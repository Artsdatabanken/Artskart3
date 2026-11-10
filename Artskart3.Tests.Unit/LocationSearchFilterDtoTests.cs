using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Enums;
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

    // Standardutvalget må holde det ufiltrerte kallet på de ferdige svarene.
    [Fact]
    public void UtelatteSkjulteFunn_ErIkkeEtAktivtFilter()
    {
        var sut = new LocationSearchFilterDto();

        sut.HiddenObservations.Should().Be(HiddenObservationFilter.Exclude);
        sut.HasActiveFilters.Should().BeFalse();
        sut.HasObservationAttributeFilters.Should().BeFalse();
    }

    [Theory]
    [InlineData(HiddenObservationFilter.Include)]
    [InlineData(HiddenObservationFilter.Only)]
    public void AndreValgForSkjulteFunn_KreverDynamiskTelling(HiddenObservationFilter value)
    {
        var sut = new LocationSearchFilterDto { HiddenObservations = value };

        sut.HasActiveFilters.Should().BeTrue();
        sut.HasObservationAttributeFilters.Should().BeTrue();
    }

    [Fact]
    public void CadastralUnitIds_KreverDynamiskTelling()
    {
        var sut = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"] };

        sut.HasActiveFilters.Should().BeTrue();
        sut.HasObservationAttributeFilters.Should().BeTrue();
    }

    [Fact]
    public void HistoricalAreaIds_KreverDynamiskTelling()
    {
        var sut = new LocationSearchFilterDto { HistoricalAreaIds = [669] };

        sut.HasActiveFilters.Should().BeTrue();
        sut.HasObservationAttributeFilters.Should().BeTrue();
        sut.HasLocationBasedAreas.Should().BeTrue();
    }

    [Fact]
    public void WithoutLocationBasedAreas_FjernerMatrikkelOgHistoriskeOmraader()
    {
        var sut = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2"], HistoricalAreaIds = [669], MunicipalityIds = ["0301"] };

        var copy = sut.WithoutLocationBasedAreas();

        copy.HasLocationBasedAreas.Should().BeFalse();
        copy.MunicipalityIds.Should().Equal("0301");
        sut.HistoricalAreaIds.Should().Equal(669);
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

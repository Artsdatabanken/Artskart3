using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Constants;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ObservationFilterValidatorTests
{
    [Fact]
    public void GetError_WithUndefinedHiddenObservations_ReturnsError()
    {
        var filter = new LocationSearchFilterDto { HiddenObservations = (HiddenObservationFilter)99 };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.HiddenObservations));
    }

    [Fact]
    public void GetError_WithValidCadastralUnitIds_ReturnsNull()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2", "0301-207/1"] };

        ObservationFilterValidator.GetError(filter).Should().BeNull();
    }

    [Fact]
    public void GetError_WithInvalidCadastralUnitId_NamesTheId()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = ["1101-1/2", "1101-1"] };

        ObservationFilterValidator.GetError(filter).Should().Contain("'1101-1'");
    }

    [Fact]
    public void GetError_WithNullCadastralUnitId_ReturnsError()
    {
        var filter = new LocationSearchFilterDto { CadastralUnitIds = [null!] };

        ObservationFilterValidator.GetError(filter).Should().NotBeNull();
    }

    [Fact]
    public void GetError_WithTooManyCadastralUnitIds_ReturnsError()
    {
        var filter = new ObservationSearchFilterDto
        {
            CadastralUnitIds = Enumerable.Range(1, SearchConstants.MaxCadastralUnitFilterSize + 1)
                .Select(n => $"1101-{n}/1")
                .ToArray()
        };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.CadastralUnitIds));
    }

    [Fact]
    public void GetError_WithTooManyHistoricalAreaIds_ReturnsError()
    {
        var filter = new LocationSearchFilterDto
        {
            HistoricalAreaIds = Enumerable.Range(1, SearchConstants.MaxHistoricalAreaFilterSize + 1).ToArray()
        };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.HistoricalAreaIds));
    }

    [Fact]
    public void GetError_WithMaxHistoricalAreaIds_ReturnsNull()
    {
        var filter = new ObservationSearchFilterDto
        {
            HistoricalAreaIds = Enumerable.Range(1, SearchConstants.MaxHistoricalAreaFilterSize).ToArray()
        };

        ObservationFilterValidator.GetError(filter).Should().BeNull();
    }

    [Theory]
    [InlineData("POLYGON ((262000 6650000, 263000 6650000, 263000 6651000, 262000 6650000))")]
    // Selvkryssende polygoner slipper gjennom; oppslaget reparerer dem.
    [InlineData("POLYGON ((0 0, 10 10, 10 0, 0 10, 0 0))")]
    public void GetError_WithValidCustomArea_ReturnsNull(string wkt)
    {
        var filter = new LocationSearchFilterDto { CustomAreaWkt = wkt };

        ObservationFilterValidator.GetError(filter).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ikke wkt")]
    [InlineData("POINT (0 0)")]
    [InlineData("MULTIPOLYGON (((0 0, 10 0, 10 10, 0 0)), ((20 20, 30 20, 30 30, 20 20)))")]
    [InlineData("POLYGON EMPTY")]
    // Ringen er ikke lukket.
    [InlineData("POLYGON ((0 0, 10 0, 10 10, 0 10))")]
    // To hjørner.
    [InlineData("POLYGON ((0 0, 10 0, 0 0))")]
    [InlineData("POLYGON ((0 0, 10 0, NaN 10, 0 0))")]
    public void GetError_WithInvalidCustomArea_ReturnsError(string wkt)
    {
        var filter = new ObservationSearchFilterDto { CustomAreaWkt = wkt };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.CustomAreaWkt));
    }

    [Fact]
    public async Task GetCustomAreaSizeErrorAsync_SaysWhenTheAreaHasTooManyLocations()
    {
        const string small = "POLYGON ((0 0, 10 0, 10 10, 0 0))";
        const string large = "POLYGON ((0 0, 1000 0, 1000 1000, 0 0))";
        var customAreas = new StubCustomAreaService(new() { [small] = [1], [large] = null });

        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto { CustomAreaWkt = small }, customAreas, CancellationToken.None)).Should().BeNull();
        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto { CustomAreaWkt = large }, customAreas, CancellationToken.None))
            .Should().Be(ObservationFilterValidator.CustomAreaTooManyLocationsMessage);
        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto(), customAreas, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Med andre områdevalg deles ikke polygonet, så hele lokasjonslisten må holde seg under
    /// grensen, ikke bare kanten.
    /// </summary>
    [Fact]
    public async Task GetCustomAreaSizeErrorAsync_WithOtherAreas_ChecksTheWholeList()
    {
        const string delt = "POLYGON ((0 0, 1000 0, 1000 1000, 0 0))";
        var customAreas = new StubCustomAreaService(
            new() { [delt] = null },
            splitsByWkt: new() { [delt] = new CustomAreaSplit([(1, "0301")], [1, 2]) });

        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto { CustomAreaWkt = delt }, customAreas, CancellationToken.None)).Should().BeNull();
        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto { CustomAreaWkt = delt, HistoricalAreaIds = [669] }, customAreas, CancellationToken.None))
            .Should().Be(ObservationFilterValidator.CustomAreaTooManyLocationsMessage);
        (await ObservationFilterValidator.GetCustomAreaSizeErrorAsync(
            new LocationSearchFilterDto { CustomAreaWkt = delt, CadastralUnitIds = ["1101-1/2"] }, customAreas, CancellationToken.None))
            .Should().Be(ObservationFilterValidator.CustomAreaTooManyLocationsMessage);
    }

    [Fact]
    public void GetError_WithTooManyCustomAreaPoints_ReturnsError()
    {
        var punkter = Enumerable.Range(0, SearchConstants.MaxCustomAreaVertices)
            .Select(i => $"{Math.Cos(i * 0.001) * 1000:0.###} {Math.Sin(i * 0.001) * 1000:0.###}")
            .Append("1000 0");
        var filter = new LocationSearchFilterDto { CustomAreaWkt = $"POLYGON (({string.Join(", ", punkter)}))" };

        ObservationFilterValidator.GetError(filter).Should().Contain(nameof(filter.CustomAreaWkt));
    }
}

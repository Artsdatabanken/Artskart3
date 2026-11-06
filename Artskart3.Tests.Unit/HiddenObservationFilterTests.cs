using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class HiddenObservationFilterTests
{
    [Theory]
    [InlineData(HiddenObservationFilter.Exclude, false)]
    [InlineData(HiddenObservationFilter.Only, true)]
    [InlineData(HiddenObservationFilter.Include, null)]
    public void RequiredHiddenValue_FollowsFilter(HiddenObservationFilter filter, bool? expected)
    {
        filter.RequiredHiddenValue().Should().Be(expected);
    }

    // Lagrede filtre og eksportjobber fra før feltet fantes mangler det i JSON-en.
    [Fact]
    public void Deserialize_WithoutField_ExcludesHiddenObservations()
    {
        JsonSerializer.Deserialize<ObservationSearchFilterDto>("{}")!
            .HiddenObservations.Should().Be(HiddenObservationFilter.Exclude);
    }

    [Theory]
    [InlineData(HiddenObservationFilter.Exclude)]
    [InlineData(HiddenObservationFilter.Include)]
    [InlineData(HiddenObservationFilter.Only)]
    public void SavedFilterJson_RoundTrips(HiddenObservationFilter value)
    {
        var json = JsonSerializer.Serialize(new ObservationSearchFilterDto { HiddenObservations = value });

        json.Should().Contain($"\"HiddenObservations\":\"{value}\"");
        JsonSerializer.Deserialize<ObservationSearchFilterDto>(json)!
            .HiddenObservations.Should().Be(value);
    }
}

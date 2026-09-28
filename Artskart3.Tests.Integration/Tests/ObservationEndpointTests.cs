using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class ObservationEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ObservationEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString, false);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-CSRF", "1");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // POST /api/Search/Observation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservations_WithEmptyFilter_Returns200WithJsonArray()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Observation", new ObservationSearchFilterDto());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetObservations_WithPageNumberZero_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 0, ResultsPerPage = 10 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithResultsPerPageTooHigh_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 99999 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithResultsPerPageZero_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 0 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithValidPagination_Returns200WithPagedStructure()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 10 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("pageNumber", out var pageNumber).Should().BeTrue();
        pageNumber.GetInt32().Should().Be(1);
        doc.RootElement.TryGetProperty("resultsPerPage", out var resultsPerPage).Should().BeTrue();
        resultsPerPage.GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetObservations_WithRestrictedAreaFilter_Returns200WithJsonArray()
    {
        var filter = new ObservationSearchFilterDto { RestrictedAreaIds = ["1"] };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetObservations_WithOceanAreaFilter_Returns200WithJsonArray()
    {
        var filter = new ObservationSearchFilterDto { OceanAreaIds = ["1"] };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // /api/Observation
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetObservationListInfo_WithValidIds_Returns200WithJsonArray()
    {
        var requestDto = new ObservationListInfoRequestDto { Ids = [953202] };
        var response = await _client.PostAsJsonAsync("/api/Search/ObservationList", requestDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var expected = await context.Observations.Where(o => o.LocationId == 953202)
            .OrderByDescending(o => o.DateTimeCollected).ThenByDescending(o => o.Id)
            .Select(o => new { o.Id, o.TaxonId, o.DateTimeCollected, Code = o.Category != null ? o.Category.Code : null })
            .ToListAsync();
        var observations = JsonSerializer.Deserialize<List<ObservationListInfoDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        observations.Select(o => o.Id).Should().Equal(expected.Select(o => o.Id));
        observations.Select(o => o.CategoryCode).Should().Equal(expected.Select(o => o.Code));
        observations.Select(o => o.TaxonId).Should().Equal(expected.Select(o => o.TaxonId));
        observations.Select(o => o.DateTimeCollected).Should().Equal(expected.Select(o => o.DateTimeCollected));
    }
}

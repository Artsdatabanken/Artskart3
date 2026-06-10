using System.Net;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class SearchEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public SearchEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString, useTestAuthentication: true);
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
    // Helsesjekk
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/hc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    // -----------------------------------------------------------------------
    // GET /api/Search/SearchTaxons
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchTaxons_WithEmptyName_Returns400()
    {
        var response = await _client.GetAsync("/api/Search/SearchTaxons?name=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchTaxons_WithMaxCountTooHigh_Returns400()
    {
        var response = await _client.GetAsync("/api/Search/SearchTaxons?name=test&maxCount=9999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchTaxons_WithValidName_Returns200WithJsonArray()
    {
        // Avhenger av at testdata inneholder minst ett takson.
        // Uten testdata returneres tom liste — fremdeles gyldig 200.
        var response = await _client.GetAsync("/api/Search/SearchTaxons?name=a&maxCount=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task SearchTaxons_ResultsRespectMaxCount()
    {
        var response = await _client.GetAsync("/api/Search/SearchTaxons?name=a&maxCount=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().BeLessThanOrEqualTo(2);
    }

    // -----------------------------------------------------------------------
    // GET /api/Search/Locations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetLocations_WithNoFilter_Returns200WithGeoJson()
    {
        var response = await _client.GetAsync("/api/Search/Locations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
    }

    [Fact]
    public async Task GetLocations_WithMaxResultsZero_Returns400()
    {
        var response = await _client.GetAsync("/api/Search/Locations?MaxResults=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLocations_WithMaxResultsTooHigh_Returns400()
    {
        var response = await _client.GetAsync("/api/Search/Locations?MaxResults=99999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLocations_WithInvertedPrecisionRange_Returns400()
    {
        var response = await _client.GetAsync(
            "/api/Search/Locations?CoordinatePrecisionFrom=1000&CoordinatePrecisionTo=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetLocations_WithMaxResults10_ReturnsAtMost10Features()
    {
        var response = await _client.GetAsync("/api/Search/Locations?MaxResults=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("features").GetArrayLength()
            .Should().BeLessThanOrEqualTo(10);
    }

    // -----------------------------------------------------------------------
    // GET /api/Search/Areas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreas_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Search/Areas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetAreas_WhenSeedDataLoaded_ReturnsAreasWithNames()
    {
        var response = await _client.GetAsync("/api/Search/Areas");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        // Med testdata: hvert område skal ha en "name"-egenskap (camelCase, ASP.NET Core standard)
        json.Should().Contain("\"name\":");
    }
}

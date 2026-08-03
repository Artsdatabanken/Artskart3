using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    // GET /api/Search/Species
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpecies_WithEmptySearch_Returns400()
    {
        var response = await _client.GetAsync("/api/Search/Species?search=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchSpecies_WithValidSearch_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Search/Species?search=test");

        // Enten 200 med resultater fra NorTaxa, eller 502 hvis NorTaxa er utilgjengelig.
        // Begge er gyldige i integrasjonstestmiljø.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadGateway);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    // -----------------------------------------------------------------------
    // POST /api/Search/Locations (GetObservationLocations action)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservationLocations_WithNoFilter_Returns200WithCompactJson()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Locations", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("epsg").GetInt32().Should().Be(25833);
        doc.RootElement.GetProperty("locations").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetObservationLocations_WithMaxResultsZero_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Locations", new { maxResults = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("error");
        json.Should().Contain("1").And.Contain("100000"); // Min and Max values
    }

    [Fact]
    public async Task GetObservationLocations_WithMaxResultsTooHigh_Returns400()
    {
        // MaxLocationResults = 100000, so 100001 exceeds the limit
        var response = await _client.PostAsJsonAsync("/api/Search/Locations", new { maxResults = 100001 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("error");
        json.Should().Contain("between").And.Contain("1").And.Contain("100000");
    }

    [Fact]
    public async Task GetObservationLocations_WithInvertedPrecisionRange_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Locations",
            new { coordinatePrecision = new { from = 1000, to = 100 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservationLocations_WithMaxResults10_ReturnsAtMost10Locations()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Locations", new { maxResults = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("locations").GetArrayLength()
            .Should().BeLessThanOrEqualTo(10);
    }

    // -----------------------------------------------------------------------
    // POST /api/Search/AreaMarkers
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreaMerkers_WithDefaultZoomLevel_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/AreaMarkers?zoomLevel=1", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetAreaMarkers_WithZoomLevel1_Returns200WithJsonArray()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/AreaMarkers?zoomLevel=1", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetAreaMarkers_WithZoomLevel2_Returns200WithJsonArray()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/AreaMarkers?zoomLevel=2", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

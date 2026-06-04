using System.Net;
using System.Text.Json;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class LookupEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LookupEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString);
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
    // GET /api/Lookup/Categories
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCategories_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetCategories_ResponseContentTypeIsApplicationJson()
    {
        var response = await _client.GetAsync("/api/Lookup/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/BasisOfRecords
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBasisOfRecords_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/BasisOfRecords");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Behaviors
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBehaviors_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Behaviors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Institutions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetInstitutions_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Institutions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Areas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreas_Returns200WithJsonObject()
    {
        var response = await _client.GetAsync("/api/Lookup/Areas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/TaxonGroups
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTaxonGroups_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonGroups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

using System.Net;
using System.Text.Json;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

public class WarningsEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WarningsWebApplicationFactory _factory;

    public WarningsEndpointTests()
    {
        _factory = new WarningsWebApplicationFactory();
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
    // GET /api/Warnings
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetWarnings_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Warnings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

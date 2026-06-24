using System.Net;
using System.Text.Json;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

public class NotificationsEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly NotificationsWebApplicationFactory _factory;

    public NotificationsEndpointTests()
    {
        _factory = new NotificationsWebApplicationFactory();
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
    // GET /api/Notifications
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetNotifications_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

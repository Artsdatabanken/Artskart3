using System.Net;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class RobotsEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public RobotsEndpointTests(DatabaseFixture db)
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

    [Fact]
    public async Task RobotsTxt_WhenIndexingNotAllowed_LeavesPagesCrawlable()
    {
        var response = await _client.GetAsync("/robots.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Disallow: /\n");
        body.Should().Contain("Disallow: /api/");
    }

    [Fact]
    public async Task AnyResponse_WhenIndexingNotAllowed_HasNoindexHeader()
    {
        var response = await _client.GetAsync("/api/Lookup/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Robots-Tag").Should().ContainSingle().Which.Should().Be("noindex");
    }
}

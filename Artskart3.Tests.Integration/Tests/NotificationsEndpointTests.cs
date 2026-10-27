using System.Net;
using System.Text.Json;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class NotificationsEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly List<Guid> _seededIds = [];

    public NotificationsEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString, false);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-CSRF", "1");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_seededIds.Count > 0)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
            await context.Set<Notification>().Where(n => _seededIds.Contains(n.Id)).ExecuteDeleteAsync();
        }

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

    [Fact]
    public async Task GetNotifications_ReturnsSeededNotificationWithAllFields()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = today.AddDays(-1);
        var end = today.AddDays(1);
        var notification = new Notification
        {
            AlertType = AlertType.Warning,
            Heading = "Planlagt vedlikehold",
            Description = "Tjenesten er utilgjengelig fredag kl. 22.",
            StartDateTime = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 1, 31, 20, 0, 0, DateTimeKind.Utc),
            StartDisplayDate = start,
            EndDisplayDate = end,
            CanClose = true
        };
        await SeedAsync(notification);

        var element = await FindNotificationAsync(notification.Id);

        element.GetProperty("id").GetGuid().Should().Be(notification.Id);
        element.GetProperty("alertType").GetInt32().Should().Be((int)AlertType.Warning);
        element.GetProperty("heading").GetString().Should().Be(notification.Heading);
        element.GetProperty("description").GetString().Should().Be(notification.Description);
        element.GetProperty("startDisplayDate").GetString().Should().Be(start.ToString("yyyy-MM-dd"));
        element.GetProperty("endDisplayDate").GetString().Should().Be(end.ToString("yyyy-MM-dd"));
        element.GetProperty("canClose").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetNotifications_NotificationWithoutDisplayDates_IsReturnedWithNullDates()
    {
        var notification = new Notification
        {
            AlertType = AlertType.Danger,
            Heading = "Tjenesten er nede",
            Description = "Vi jobber med saken.",
            CanClose = false
        };
        await SeedAsync(notification);

        var element = await FindNotificationAsync(notification.Id);

        element.GetProperty("alertType").GetInt32().Should().Be((int)AlertType.Danger);
        element.GetProperty("startDisplayDate").ValueKind.Should().Be(JsonValueKind.Null);
        element.GetProperty("endDisplayDate").ValueKind.Should().Be(JsonValueKind.Null);
        element.GetProperty("canClose").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetNotifications_EachSeededNotificationGetsUniqueGuid()
    {
        var first = new Notification { AlertType = AlertType.Info, Heading = "Første", Description = "Første" };
        var second = new Notification { AlertType = AlertType.Success, Heading = "Andre", Description = "Andre" };
        await SeedAsync(first, second);

        first.Id.Should().NotBeEmpty();
        second.Id.Should().NotBeEmpty().And.NotBe(first.Id);

        (await FindNotificationAsync(first.Id)).GetProperty("heading").GetString().Should().Be("Første");
        (await FindNotificationAsync(second.Id)).GetProperty("heading").GetString().Should().Be("Andre");
    }

    // Offsets i dager fra i dag; null betyr at datoen mangler (åpen ende).
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(-5, 5, true)]
    [InlineData(0, 0, true)]
    [InlineData(null, 3, true)]
    [InlineData(-3, null, true)]
    [InlineData(-10, -1, false)]
    [InlineData(null, -1, false)]
    [InlineData(1, 10, false)]
    [InlineData(1, null, false)]
    public async Task GetNotifications_FiltersOnDisplayPeriod(int? startOffsetDays, int? endOffsetDays, bool expectedVisible)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var notification = new Notification
        {
            AlertType = AlertType.Info,
            Heading = "Visningsperiode",
            Description = "Visningsperiode",
            StartDisplayDate = startOffsetDays is { } s ? today.AddDays(s) : null,
            EndDisplayDate = endOffsetDays is { } e ? today.AddDays(e) : null
        };
        await SeedAsync(notification);

        var ids = await GetNotificationIdsAsync();

        ids.Contains(notification.Id).Should().Be(expectedVisible);
    }

    private async Task SeedAsync(params Notification[] notifications)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        context.Set<Notification>().AddRange(notifications);
        await context.SaveChangesAsync();
        _seededIds.AddRange(notifications.Select(n => n.Id));
    }

    private async Task<HashSet<Guid>> GetNotificationIdsAsync()
    {
        var response = await _client.GetAsync("/api/Notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid())
            .ToHashSet();
    }

    private async Task<JsonElement> FindNotificationAsync(Guid id)
    {
        var response = await _client.GetAsync("/api/Notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == id)
            .Clone();
    }
}

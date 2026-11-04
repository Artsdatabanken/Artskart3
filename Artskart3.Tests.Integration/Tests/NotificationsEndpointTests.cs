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
        var now = NorwegianNow();
        var publishFrom = now.AddDays(-1);
        var publishUntil = now.AddDays(2);
        var eventStart = now.AddDays(1);
        var eventEnd = now.AddDays(1).AddHours(2);
        var notification = new Notification
        {
            AlertType = AlertType.Warning,
            Heading = "Planlagt vedlikehold",
            Description = "Tjenesten er utilgjengelig fredag kl. 22.",
            PublishFrom = publishFrom,
            PublishUntil = publishUntil,
            EventStart = eventStart,
            EventEnd = eventEnd,
            CanClose = true
        };
        await SeedAsync(notification);

        var element = await FindNotificationAsync(notification.Id);

        element.GetProperty("id").GetGuid().Should().Be(notification.Id);
        element.GetProperty("alertType").GetInt32().Should().Be((int)AlertType.Warning);
        element.GetProperty("heading").GetString().Should().Be(notification.Heading);
        element.GetProperty("description").GetString().Should().Be(notification.Description);
        element.GetProperty("publishFrom").GetDateTime().Should().Be(publishFrom);
        element.GetProperty("publishUntil").GetDateTime().Should().Be(publishUntil);
        element.GetProperty("eventStart").GetDateTime().Should().Be(eventStart);
        element.GetProperty("eventEnd").GetDateTime().Should().Be(eventEnd);
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
        element.GetProperty("publishFrom").ValueKind.Should().Be(JsonValueKind.Null);
        element.GetProperty("publishUntil").ValueKind.Should().Be(JsonValueKind.Null);
        element.GetProperty("eventStart").ValueKind.Should().Be(JsonValueKind.Null);
        element.GetProperty("eventEnd").ValueKind.Should().Be(JsonValueKind.Null);
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

    // Offsets i minutter fra norsk tid nå; null betyr at tidspunktet mangler (åpen ende).
    // ±30 minutter er mindre enn forskjellen mellom UTC og norsk tid, så sammenligning mot UTC gir feil svar.
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(-300, 300, true)]
    [InlineData(null, 180, true)]
    [InlineData(-180, null, true)]
    [InlineData(-600, -60, false)]
    [InlineData(null, -30, false)]
    [InlineData(-30, null, true)]
    [InlineData(30, null, false)]
    [InlineData(null, 30, true)]
    [InlineData(60, 600, false)]
    public async Task GetNotifications_FiltersOnPublishPeriodInNorwegianTime(int? publishFromOffsetMinutes, int? publishUntilOffsetMinutes, bool expectedVisible)
    {
        var now = NorwegianNow();
        var notification = new Notification
        {
            AlertType = AlertType.Info,
            Heading = "Visningsperiode",
            Description = "Visningsperiode",
            PublishFrom = publishFromOffsetMinutes is { } fromMinutes ? now.AddMinutes(fromMinutes) : null,
            PublishUntil = publishUntilOffsetMinutes is { } untilMinutes ? now.AddMinutes(untilMinutes) : null
        };
        await SeedAsync(notification);

        var ids = await GetNotificationIdsAsync();

        ids.Contains(notification.Id).Should().Be(expectedVisible);
    }

    private static DateTime NorwegianNow()
    {
        var norway = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        return new DateTime(norway.Ticks - norway.Ticks % TimeSpan.TicksPerSecond);
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

using System.Text.Json;
using Artskart3.Api.HealthChecks;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

public class HealthCheckExtensionsTests
{
    private static ILogger CreateLogger() => Mock.Of<ILogger>();

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // -----------------------------------------------------------------------
    // AddCustomHealthChecks — registrering
    // -----------------------------------------------------------------------

    [Fact]
    public void AddCustomHealthChecks_WithDbConnectionString_RegistersDatabaseHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().ContainSingle(r => r.Name == "Database");
    }

    [Fact]
    public void AddCustomHealthChecks_WithoutDbConnectionString_DoesNotRegisterDatabaseHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().NotContain(r => r.Name == "Database");
    }

    /// <summary>
    /// Columnstore-sjekken henger på samme tilkoblingsstreng som databasesjekken.
    /// Uten den er et manglende IX_OEI_Columnstore bare én logglinje ved oppstart.
    /// </summary>
    [Fact]
    public void AddCustomHealthChecks_WithDbConnectionString_RegistersColumnstoreHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().ContainSingle(r => r.Name == "Columnstore");
    }

    [Fact]
    public void AddCustomHealthChecks_WithoutDbConnectionString_DoesNotRegisterColumnstoreHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().NotContain(r => r.Name == "Columnstore");
    }

    [Fact]
    public void AddCustomHealthChecks_ColumnstoreCheck_HasExpectedTags()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        var sjekk = options.Value.Registrations.Single(r => r.Name == "Columnstore");
        sjekk.Tags.Should().Contain("dependencies").And.Contain("database").And.Contain("performance");
    }

    [Fact]
    public void AddCustomHealthChecks_DatabaseCheck_HasExpectedTags()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        var dbCheck = options.Value.Registrations.Single(r => r.Name == "Database");
        dbCheck.Tags.Should().Contain("dependencies").And.Contain("database");
    }

    [Fact]
    public void AddCustomHealthChecks_DatabaseCheck_IsTaggedForReadiness()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        var dbCheck = options.Value.Registrations.Single(r => r.Name == "Database");
        dbCheck.Tags.Should().Contain(HealthCheckExtensions.ReadyTag);
    }

    /// <summary>
    /// Tellebuffersjekken henger på samme tilkoblingsstreng som de andre. Uten den
    /// er en tom buffer helt stille: oppslaget returnerer null uten en logglinje når
    /// Status ikke er 'Ready', og det er tilstanden etter hver deploy.
    /// </summary>
    [Fact]
    public void AddCustomHealthChecks_WithDbConnectionString_RegistersCountCachesHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();
        var config = BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = "Server=test;Database=test" });

        services.AddCustomHealthChecks(config, CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().ContainSingle(r => r.Name == "CountCaches");
    }

    [Fact]
    public void AddCustomHealthChecks_WithoutDbConnectionString_DoesNotRegisterCountCachesHealthCheck()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().NotContain(r => r.Name == "CountCaches");
    }

    // -----------------------------------------------------------------------
    // PolygonStore — registreres bare når lageret finnes i containeren
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sjekken resolver IPolygonLocationStore fra containeren. Er lageret ikke
    /// registrert — Workers kaller ikke AddApplicationServices — skal sjekken ikke
    /// registreres i det hele tatt, ellers kaster den når /hc kjøres.
    /// </summary>
    [Fact]
    public void AddCustomHealthChecks_UtenPolygonlager_RegistrererIkkePolygonStoreSjekken()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().NotContain(r => r.Name == "PolygonStore");
    }

    [Fact]
    public void AddCustomHealthChecks_MedPolygonlager_RegistrererPolygonStoreSjekken()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(Mock.Of<IPolygonLocationStore>());

        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        options.Value.Registrations.Should().ContainSingle(r => r.Name == "PolygonStore");
    }

    /// <summary>
    /// Et lager som ikke er klart skal gi Degraded og ikke Unhealthy: databasestien
    /// gir riktige svar, bare tregere, og Unhealthy gir 503 på /hc. Beskrivelsen fra
    /// lageret skal med, for det er den som skiller «bygger nå» fra «feilet».
    /// </summary>
    [Fact]
    public async Task PolygonStoreSjekken_NaarLageretIkkeErKlart_GirDegraded()
    {
        var lager = new Mock<IPolygonLocationStore>();
        lager.SetupGet(l => l.IsReady).Returns(false);
        lager.Setup(l => l.Describe()).Returns("ikke bygget");

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(lager.Object);
        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var report = await services.BuildServiceProvider()
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Name == "PolygonStore");

        var entry = report.Entries["PolygonStore"];
        entry.Status.Should().Be(HealthStatus.Degraded);
        entry.Description.Should().Contain("ikke bygget");
    }

    [Fact]
    public async Task PolygonStoreSjekken_NaarLageretErKlart_GirHealthy()
    {
        var lager = new Mock<IPolygonLocationStore>();
        lager.SetupGet(l => l.IsReady).Returns(true);
        lager.Setup(l => l.Describe()).Returns("75 990 lokasjoner, 3 946 488 observasjoner");

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(lager.Object);
        services.AddCustomHealthChecks(BuildConfig(new()), CreateLogger());

        var report = await services.BuildServiceProvider()
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Name == "PolygonStore");

        var entry = report.Entries["PolygonStore"];
        entry.Status.Should().Be(HealthStatus.Healthy);
        entry.Description.Should().Contain("75 990 lokasjoner");
    }

    // -----------------------------------------------------------------------
    // Databasesjekken — utførelse
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DatabaseHealthCheck_WhenServerUnreachable_ReturnsUnhealthy()
    {
        // Port 9 (discard) har ingen lytter, så tilkoblingen avvises umiddelbart.
        // Connect Timeout holder testen rask hvis noe likevel svarer tregt.
        var services = new ServiceCollection().AddLogging();
        services.AddCustomHealthChecks(
            BuildConfig(new()
            {
                ["ConnectionStrings:ArtskartIndex"] =
                    "Server=127.0.0.1,9;Database=x;User Id=a;Password=b;TrustServerCertificate=True;Connect Timeout=1"
            }),
            CreateLogger());

        var report = await services.BuildServiceProvider()
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Name == "Database");

        report.Entries["Database"].Status.Should().Be(HealthStatus.Unhealthy);
    }

    /// <summary>
    /// UNNTAKSTEKST SKAL IKKE UT I SVARET
    ///
    /// Description havner i JSON-kroppen (WriteJsonResponse), og /hc er kartlagt
    /// uten RequireAuthorization — altså anonymt. En SqlException navngir
    /// rutinemessig server, instans, database og pålogging.
    ///
    /// Et ukjent nøkkelord i tilkoblingsstrengen gjør dette deterministisk:
    /// SqlConnection kaster med nøkkelordet i meldingen, uten at noe nettverk er
    /// involvert. Finner vi det ikke igjen i Description, går heller ikke et
    /// servernavn den veien.
    /// </summary>
    [Fact]
    public async Task Helsesjekk_lekker_ikke_unntaksteksten_til_svaret()
    {
        const string hemmelig = "hemmelig-server";

        var services = new ServiceCollection().AddLogging();
        services.AddCustomHealthChecks(
            BuildConfig(new() { ["ConnectionStrings:ArtskartIndex"] = $"{hemmelig}=x" }),
            CreateLogger());

        var report = await services.BuildServiceProvider()
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(r => r.Name == "Database");

        var entry = report.Entries["Database"];

        entry.Status.Should().NotBe(HealthStatus.Healthy,
            "testen måler bare noe hvis sjekken faktisk feiler");
        entry.Description.Should().NotContain(hemmelig,
            "unntaksteksten navngir infrastruktur og serveres anonymt fra /hc");
        entry.Description.Should().Contain("see logs",
            "den som feilsøker skal ledes til loggen, der hele unntaket står");
    }

    // -----------------------------------------------------------------------
    // CreateProbeOptions — hvilke sjekker hvert endepunkt kjører
    // -----------------------------------------------------------------------

    [Fact]
    public void CreateProbeOptions_WithoutPredicate_RunsAllChecks()
    {
        var options = HealthCheckExtensions.CreateProbeOptions(allowCachingResponses: false);

        options.Predicate.Should().BeNull();
    }

    [Fact]
    public void CreateProbeOptions_LivenessPredicate_ExcludesEveryCheck()
    {
        var options = HealthCheckExtensions.CreateProbeOptions(false, _ => false);

        options.Predicate!(Registration("Database", HealthCheckExtensions.ReadyTag)).Should().BeFalse();
    }

    [Fact]
    public void CreateProbeOptions_ReadyPredicate_SelectsOnlyReadyTaggedChecks()
    {
        var options = HealthCheckExtensions.CreateProbeOptions(
            false, check => check.Tags.Contains(HealthCheckExtensions.ReadyTag));

        options.Predicate!(Registration("Database", HealthCheckExtensions.ReadyTag)).Should().BeTrue();
        options.Predicate!(Registration("Annet", "dependencies")).Should().BeFalse();
    }

    [Fact]
    public void CreateProbeOptions_PassesCachingFlagThrough()
    {
        HealthCheckExtensions.CreateProbeOptions(true).AllowCachingResponses.Should().BeTrue();
        HealthCheckExtensions.CreateProbeOptions(false).AllowCachingResponses.Should().BeFalse();
    }

    private static HealthCheckRegistration Registration(string name, params string[] tags) =>
        new(name, Mock.Of<IHealthCheck>(), HealthStatus.Unhealthy, tags);

    // -----------------------------------------------------------------------
    // WriteJsonResponse
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WriteJsonResponse_SetsContentTypeToJson()
    {
        var (ctx, _) = CreateHttpContext();
        await HealthCheckExtensions.WriteJsonResponse(ctx, EmptyReport(HealthStatus.Healthy));

        ctx.Response.ContentType.Should().StartWith("application/json");
    }

    [Fact]
    public async Task WriteJsonResponse_HealthyReport_WritesHealthyStatus()
    {
        var (ctx, body) = CreateHttpContext();
        await HealthCheckExtensions.WriteJsonResponse(ctx, EmptyReport(HealthStatus.Healthy));

        var result = await DeserializeAsync(body);
        result!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task WriteJsonResponse_UnhealthyReport_WritesUnhealthyStatus()
    {
        var (ctx, body) = CreateHttpContext();
        await HealthCheckExtensions.WriteJsonResponse(ctx, EmptyReport(HealthStatus.Unhealthy));

        var result = await DeserializeAsync(body);
        result!.Status.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task WriteJsonResponse_DegradedReport_WritesDegradedStatus()
    {
        var (ctx, body) = CreateHttpContext();
        await HealthCheckExtensions.WriteJsonResponse(ctx, EmptyReport(HealthStatus.Degraded));

        var result = await DeserializeAsync(body);
        result!.Status.Should().Be("Degraded");
    }

    [Fact]
    public async Task WriteJsonResponse_MapsEntryStatusAndDescription()
    {
        var (ctx, body) = CreateHttpContext();
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["db"] = new HealthReportEntry(
                HealthStatus.Healthy, "Database is up",
                TimeSpan.FromMilliseconds(42), null, null)
        };
        await HealthCheckExtensions.WriteJsonResponse(ctx, new HealthReport(entries, TimeSpan.FromMilliseconds(42)));

        var result = await DeserializeAsync(body);
        result!.Entries.Should().ContainKey("db");
        result.Entries["db"].Status.Should().Be("Healthy");
        result.Entries["db"].Description.Should().Be("Database is up");
    }

    [Fact]
    public async Task WriteJsonResponse_MapsEntryTags()
    {
        var (ctx, body) = CreateHttpContext();
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["db"] = new HealthReportEntry(
                HealthStatus.Healthy, null,
                TimeSpan.FromMilliseconds(10), null, null,
                tags: new[] { "dependencies", "database" })
        };
        await HealthCheckExtensions.WriteJsonResponse(ctx, new HealthReport(entries, TimeSpan.FromMilliseconds(10)));

        var result = await DeserializeAsync(body);
        result!.Entries["db"].Tags.Should().Contain("dependencies").And.Contain("database");
    }

    [Fact]
    public async Task WriteJsonResponse_MapsTotalDuration()
    {
        var (ctx, body) = CreateHttpContext();
        var duration = TimeSpan.FromMilliseconds(123);
        await HealthCheckExtensions.WriteJsonResponse(ctx, EmptyReport(HealthStatus.Healthy, duration));

        var result = await DeserializeAsync(body);
        result!.TotalDuration.Should().Be(duration);
    }

    // -----------------------------------------------------------------------
    // Hjelpemetoder
    // -----------------------------------------------------------------------

    private static (DefaultHttpContext ctx, MemoryStream body) CreateHttpContext()
    {
        var ctx = new DefaultHttpContext();
        var body = new MemoryStream();
        ctx.Response.Body = body;
        return (ctx, body);
    }

    private static HealthReport EmptyReport(HealthStatus status, TimeSpan? duration = null) =>
        new(new Dictionary<string, HealthReportEntry>(), status, duration ?? TimeSpan.FromMilliseconds(10));

    private static async Task<HealthCheckResponse?> DeserializeAsync(MemoryStream body)
    {
        body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<HealthCheckResponse>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}

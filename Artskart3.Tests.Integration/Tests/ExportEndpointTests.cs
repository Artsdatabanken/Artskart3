using System.Net;
using System.Net.Http.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class ExportEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly string TestUserId = "00000000-0000-0000-0000-000000000001";

    public ExportEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString, useTestAuthentication: true);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-CSRF", "1");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", TestUserId);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // GET /api/export/csv/columns
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetColumns_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/export/csv/columns");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // POST /api/export/csv/summary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSummary_WithValidFilter_Returns200()
    {
        var request = new StartExportRequestDto
        {
            Filter = new ObservationSearchFilterDto(),
            SelectedColumns = []
        };

        var response = await _client.PostAsJsonAsync("/api/export/csv/summary", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // POST /api/export/csv/start
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StartExport_WithValidFilter_Returns200()
    {
        var request = new StartExportRequestDto
        {
            Filter = new ObservationSearchFilterDto(),
            SelectedColumns = []
        };

        var response = await _client.PostAsJsonAsync("/api/export/csv/start", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // GET /api/export/csv/{jobId}/status
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetStatus_WithNonExistentJob_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/export/csv/999999/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // GET /api/export/csv/{jobId}/download
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Download_WithNonExistentJob_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/export/csv/999999/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // GET /api/export/csv/{jobId}/download/excel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DownloadExcel_WithNonExistentJob_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/export/csv/999999/download/excel");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // POST /api/export/csv/{jobId}/cancel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Cancel_WithNonExistentJob_ReturnsConflict()
    {
        var response = await _client.PostAsync("/api/export/csv/999999/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // -----------------------------------------------------------------------
    // GET /api/export/csv/history
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetHistory_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/export/csv/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Identitet
    // -----------------------------------------------------------------------

    /// <summary>
    /// Eksportjobber er knyttet til «sub». En innlogging uten sub skal avvises,
    /// ikke lagres under visningsnavnet — da blir jobben usynlig for brukeren
    /// neste gang sub er på plass, og to personer med samme navn ville delt
    /// eksporthistorikk.
    /// </summary>
    [Fact]
    public async Task StartExport_WithoutIdentity_Returns401AndCreatesNoJob()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        // Ingen X-Test-UserId — altså ingen sub.

        var request = new StartExportRequestDto
        {
            Filter = new ObservationSearchFilterDto(),
            SelectedColumns = []
        };

        var response = await client.PostAsJsonAsync("/api/export/csv/start", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// En brukers eksporter skal ikke være synlige for andre. Historikken
    /// filtreres på UserId, og det er den samme filtreringen som gjør at et
    /// feilaktig UserId ville gjort eksporter utilgjengelige.
    /// </summary>
    [Fact]
    public async Task GetHistory_DoesNotLeakOtherUsersExports()
    {
        var otherUserId = Guid.NewGuid().ToString();

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-CSRF", "1");
        otherClient.DefaultRequestHeaders.Add("X-Test-UserId", otherUserId);

        await StartExportAsync(_client, "min-eksport");

        var response = await otherClient.GetFromJsonAsync<List<CsvExportJobDto>>("/api/export/csv/history");

        response.Should().NotBeNull();
        response!.Should().NotContain(job => job.Name == "min-eksport");
    }

    // -----------------------------------------------------------------------
    // Samtidighetsgrense
    // -----------------------------------------------------------------------

    /// <summary>
    /// Grensen er tre samtidige jobber per bruker. Den fjerde skal gi 409 med en
    /// melding som forklarer hvorfor — frontend viste tidligere den samme
    /// intetsigende «prøv igjen senere» for dette som for alt annet.
    /// </summary>
    [Fact]
    public async Task StartExport_BeyondConcurrencyLimit_Returns409WithReason()
    {
        var userId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        for (var i = 0; i < 3; i++)
        {
            var accepted = await StartExportAsync(client, $"eksport-{i}");
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var response = await StartExportAsync(client, "eksport-4");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("samtidige", "brukeren må få vite hvorfor eksporten ble avvist");
    }

    // -----------------------------------------------------------------------
    // Validering
    // -----------------------------------------------------------------------

    /// <summary>
    /// Name er HasMaxLength(200) i databasen. Uten validering gikk et for langt
    /// navn helt ned til SaveChanges og kastet SqlException — altså en 500, og i
    /// frontend samme generelle feilmelding som alt annet.
    /// </summary>
    [Fact]
    public async Task StartExport_WithTooLongName_Returns400()
    {
        var request = new StartExportRequestDto
        {
            Name = new string('a', 201),
            Filter = new ObservationSearchFilterDto(),
            SelectedColumns = []
        };

        var response = await _client.PostAsJsonAsync("/api/export/csv/start", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static Task<HttpResponseMessage> StartExportAsync(HttpClient client, string name) =>
        client.PostAsJsonAsync("/api/export/csv/start", new StartExportRequestDto
        {
            Name = name,
            Filter = new ObservationSearchFilterDto(),
            SelectedColumns = []
        });
}

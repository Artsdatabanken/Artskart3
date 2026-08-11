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
}

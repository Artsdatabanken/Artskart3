using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class ObservationEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ObservationEndpointTests(DatabaseFixture db)
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

    // -----------------------------------------------------------------------
    // POST /api/Search/ObservationImages
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservationImages_WithoutPagination_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/ObservationImages", new ObservationSearchFilterDto());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservationImages_ReturnsViewableImagesOfFilteredObservationsNewestFirst()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var observations = await context.Observations.Where(o => !o.IsDeleted).OrderBy(o => o.Id).Take(3).ToListAsync();
        var imageType = new MediaFileType { Id = 987660, MediaTypeName = "Image", MimeType = "image/jpeg" };
        var documentType = new MediaFileType { Id = 987661, MediaTypeName = "Document", MimeType = "application/pdf" };
        MediaFile Media(Observation observation, MediaFileType type, bool deleted = false) => new()
        {
            ObservationId = observation.Id, Origin = $"https://example.org/{observation.Id}.jpg",
            MediaFileType = type, Downloaded = false, IsDeleted = deleted, RightsHolder = "Photographer",
        };
        var viewable = new[] { Media(observations[0], imageType), Media(observations[0], imageType), Media(observations[1], imageType) };
        var hidden = new[] { Media(observations[1], imageType, deleted: true), Media(observations[1], documentType), Media(observations[2], imageType) };
        context.MediaFiles.AddRange(viewable.Concat(hidden));
        var hadMediaFiles = observations.ToDictionary(o => o.Id, o => o.HasMediaFiles);
        observations.ForEach(o => o.HasMediaFiles = true);
        await context.SaveChangesAsync();
        try
        {
            var filter = new ObservationSearchFilterDto
            {
                ObservationIds = [observations[0].Id, observations[1].Id], PageNumber = 1, ResultsPerPage = 2,
            };
            var firstPage = await PostObservationImagesAsync(filter);
            filter.PageNumber = 2;
            var secondPage = await PostObservationImagesAsync(filter);

            var expectedOrder = observations.Take(2)
                .OrderByDescending(o => o.DateTimeCollected).ThenByDescending(o => o.Id)
                .SelectMany(o => viewable.Where(m => m.ObservationId == o.Id).OrderBy(m => m.Id))
                .Select(m => m.Id);
            firstPage.Items!.Concat(secondPage.Items!).Select(i => i.Image.Id).Should().Equal(expectedOrder);
            firstPage.LookaheadCount.Should().Be(1);
            firstPage.HasMorePages.Should().BeFalse();
            firstPage.Items!.Should().OnlyContain(i => i.Image.RightsHolder == "Photographer" && !i.Image.HasStoredImage);
            var first = observations.Single(o => o.Id == firstPage.Items!.First().ObservationId);
            firstPage.Items!.First().CoordinatePrecision.Should().Be(first.CoordinatePrecisionInMeters);
            var point = new ObservationPointDto(first.Latitude, first.Longitude, first.East, first.North);
            firstPage.Items!.First().Point.Should().Be(ObservationPointValidator.IsUsable(point) ? point : null);
            var author = await context.Set<Taxon>().Where(t => t.Id == first.TaxonId).Select(t => t.ValidScientificNameAuthorship).SingleAsync();
            firstPage.Items!.First().Author.Should().Be(author);
        }
        finally
        {
            context.MediaFiles.RemoveRange(viewable.Concat(hidden));
            context.Set<MediaFileType>().RemoveRange(imageType, documentType);
            observations.ForEach(o => o.HasMediaFiles = hadMediaFiles[o.Id]);
            await context.SaveChangesAsync();
        }
    }

    private async Task<PagedObservationImageResponseDto> PostObservationImagesAsync(ObservationSearchFilterDto filter)
    {
        var response = await _client.PostAsJsonAsync("/api/Search/ObservationImages", filter);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedObservationImageResponseDto>())!;
    }

    // -----------------------------------------------------------------------
    // POST /api/Search/Observation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetObservations_WithEmptyFilter_Returns200WithJsonArray()
    {
        var response = await _client.PostAsJsonAsync("/api/Search/Observation", new ObservationSearchFilterDto());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetObservations_WithPageNumberZero_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 0, ResultsPerPage = 10 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithResultsPerPageTooHigh_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 99999 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithResultsPerPageZero_Returns400()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 0 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetObservations_WithValidPagination_Returns200WithPagedStructure()
    {
        var filter = new ObservationSearchFilterDto { PageNumber = 1, ResultsPerPage = 10 };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("pageNumber", out var pageNumber).Should().BeTrue();
        pageNumber.GetInt32().Should().Be(1);
        doc.RootElement.TryGetProperty("resultsPerPage", out var resultsPerPage).Should().BeTrue();
        resultsPerPage.GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetObservations_WithRestrictedAreaFilter_Returns200WithJsonArray()
    {
        var filter = new ObservationSearchFilterDto { RestrictedAreaIds = ["1"] };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetObservations_WithOceanAreaFilter_Returns200WithJsonArray()
    {
        var filter = new ObservationSearchFilterDto { OceanAreaIds = ["1"] };

        var response = await _client.PostAsJsonAsync("/api/Search/Observation", filter);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // /api/Observation
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetObservationListInfo_WithValidIds_Returns200WithJsonArray()
    {
        var requestDto = new ObservationListInfoRequestDto { Ids = [953202] };
        var response = await _client.PostAsJsonAsync("/api/Search/ObservationList", requestDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var expected = await context.Observations.Where(o => o.LocationId == 953202)
            .OrderByDescending(o => o.DateTimeCollected).ThenByDescending(o => o.Id)
            .Select(o => new
            {
                o.Id,
                o.TaxonId,
                o.DateTimeCollected,
                Code = o.Category != null ? o.Category.Code : null,
                o.Location!.CoordinatePrecision,
            })
            .ToListAsync();
        var observations = JsonSerializer.Deserialize<List<ObservationListInfoDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        observations.Select(o => o.Id).Should().Equal(expected.Select(o => o.Id));
        observations.Select(o => o.CategoryCode).Should().Equal(expected.Select(o => o.Code));
        observations.Select(o => o.TaxonId).Should().Equal(expected.Select(o => o.TaxonId));
        observations.Select(o => o.DateTimeCollected).Should().Equal(expected.Select(o => o.DateTimeCollected));
        observations.Select(o => o.CoordinatePrecision).Should().Equal(expected.Select(o => o.CoordinatePrecision));
    }
}

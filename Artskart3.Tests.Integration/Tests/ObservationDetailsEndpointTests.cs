using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public sealed class ObservationDetailsEndpointTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory factory = new(db.ConnectionString, false);
    private HttpClient client = null!;
    public Task InitializeAsync()
    {
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    private const int ObservationId = 8368071;

    [Fact]
    public async Task Get_ProjectsPublicCoordinatesAndMediaWithoutBinaryPayload()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var observation = await context.Observations.SingleAsync(o => o.Id == ObservationId);
        var (media, type) = await AddStoredImageAsync(context);
        var sensitive = new SensitiveObservationDatum
        {
            ObservationId = ObservationId, Latitude = 61.123456, Longitude = 11.123456,
            East = 290000, North = 6900000, Locality = "Private locality",
        };
        context.Set<SensitiveObservationDatum>().Add(sensitive);
        await context.SaveChangesAsync();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/observations/{ObservationId}");
            request.Headers.Add("X-CSRF", "1");
            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await response.Content.ReadFromJsonAsync<ObservationDetailDto>();
            detail!.Point.Should().Be(new ObservationPointDto(observation.Latitude, observation.Longitude, observation.East, observation.North));
            detail.LocationId.Should().Be(observation.LocationId);
            detail.Images.Should().ContainSingle(m => m.Id == media.Id && m.HasStoredImage);
            var author = await context.Set<Taxon>().Where(t => t.Id == observation.TaxonId).Select(t => t.ValidScientificNameAuthorship).SingleAsync();
            detail.Author.Should().Be(author);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContain("iVBORw").And.NotContain("base64").And.NotContain("sensitive")
                .And.NotContain("Private locality").And.NotContain("61.123456");
        }
        finally
        {
            context.Set<SensitiveObservationDatum>().Remove(sensitive);
            await RemoveStoredImageAsync(context, media, type);
        }
    }

    [Fact]
    public async Task FindByOccurrenceId_MatchesOccurrenceIdOrProxyId()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var observation = await context.Observations.SingleAsync(o => o.Id == ObservationId);
        var (originalOccurrenceId, originalProxyId) = (observation.OccurrenceId, observation.ProxyId);
        // Samme form som NINA-data: GUID-en i OccurrenceId, kildekvalifisert verdi i ProxyId.
        observation.OccurrenceId = "0008EF28-TEST-OCCURRENCE-LINK";
        observation.ProxyId = "nina/skandobs/0008ef28-test-occurrence-link";
        await context.SaveChangesAsync();
        try
        {
            // Angular sender skråstrek ukodet i spørrestrengen, eksterne lenker sender %2F. Store og
            // små bokstaver matcher bare gjennom databasens sortering, som InMemory ikke har.
            foreach (var encoded in new[]
                     {
                         observation.OccurrenceId, observation.OccurrenceId.ToLowerInvariant(),
                         observation.ProxyId, Uri.EscapeDataString(observation.ProxyId.ToUpperInvariant()),
                     })
            {
                var found = await GetByOccurrenceIdAsync(encoded);
                found.StatusCode.Should().Be(HttpStatusCode.OK, encoded);
                (await found.Content.ReadFromJsonAsync<int>()).Should().Be(ObservationId);
            }
            (await GetByOccurrenceIdAsync("nina%2Fskandobs%2F0008ef28-test")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await GetByOccurrenceIdAsync("")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            (observation.OccurrenceId, observation.ProxyId) = (originalOccurrenceId, originalProxyId);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Image_ReturnsStoredBytesWithoutCsrfHeaderAndHidesForeignOrDeletedMedia()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var (media, type) = await AddStoredImageAsync(context);
        try
        {
            var imageResponse = await client.GetAsync($"/api/observations/{ObservationId}/media/{media.Id}/image");
            imageResponse.StatusCode.Should().Be(HttpStatusCode.OK, "public images do not require a CSRF header");
            (await imageResponse.Content.ReadAsByteArrayAsync()).Should().Equal(media.Image);
            (await client.GetAsync($"/api/observations/8373539/media/{media.Id}/image")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            media.IsDeleted = true;
            await context.SaveChangesAsync();
            (await client.GetAsync($"/api/observations/{ObservationId}/media/{media.Id}/image")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await RemoveStoredImageAsync(context, media, type);
        }
    }

    /// <summary>
    /// Annoteringstjenesten byttes ut med en stub. Testen sjekker at oppslaget av egenskapene
    /// (datasett via NodeId, matchet navn, gjeldende fylke) lar seg oversette mot SQL Server.
    /// </summary>
    [Fact]
    public async Task Annotations_SendsObservationPropertiesAndReturnsTheComment()
    {
        var handler = new CapturingHandler("""
            [{ "id": 837, "comment": "Uten dokumentasjon og forkastes.", "authorName": "Reidar Elven",
               "dateCreated": "2024-09-17T17:41:49", "fields": [], "actions": { "acceptChanges": true },
               "observations": [{ "observationProxyId": "x" }] }]
            """);
        await using var stubbed = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient<IObservationAnnotationService, ObservationAnnotationService>()
                .ConfigurePrimaryHttpMessageHandler(() => handler)));
        using var stubbedClient = stubbed.CreateClient();
        stubbedClient.DefaultRequestHeaders.Add("X-CSRF", "1");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var observation = await context.Observations.SingleAsync(o => o.Id == ObservationId);
        var scientificName = await context.Set<TaxonName>()
            .Where(n => n.Id == observation.MatchedScientificNameId).Select(n => n.ScientificName).SingleAsync();
        var county = await context.Set<Location>().Where(l => l.Id == observation.LocationId)
            .SelectMany(l => l.Areas).Where(a => !a.IsDeleted && a.IsCurrent && a.AreaTypeId == 2)
            .OrderBy(a => a.Name).Select(a => a.Name).FirstOrDefaultAsync();
        var datasetId = await context.Set<ImportState>().Where(s => s.Id == observation.NodeId)
            .Select(s => (Guid?)s.GbifDataSetId).FirstOrDefaultAsync();

        var response = await stubbedClient.GetAsync($"/api/observations/{ObservationId}/annotations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var annotations = await response.Content.ReadFromJsonAsync<List<ObservationAnnotationDto>>();
        annotations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Author = "Reidar Elven", Comment = "Uten dokumentasjon og forkastes.", Changes = Array.Empty<object>(),
        });
        handler.RequestUri!.AbsolutePath.Should().EndWith("/annotationapi/api/public-annotations/forObservation");
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("occurrenceId").GetString().Should().Be(observation.ProxyId ?? "");
        body.RootElement.GetProperty("scientificName").GetString().Should().Be(scientificName);
        body.RootElement.GetProperty("stateProvince").GetString().Should().Be(county ?? "");
        body.RootElement.GetProperty("gbifDataSetId").GetGuid().Should().Be(datasetId ?? Guid.Empty);

        (await stubbedClient.GetAsync("/api/observations/999999999/annotations")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Map_InvalidPresetIsRejectedWithoutCsrfHeader()
    {
        (await client.GetAsync($"/api/observations/{ObservationId}/maps/arbitrary")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> GetByOccurrenceIdAsync(string encodedOccurrenceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/observations/by-occurrence-id?occurrenceId={encodedOccurrenceId}");
        request.Headers.Add("X-CSRF", "1");
        return await client.SendAsync(request);
    }

    private static async Task<(MediaFile Media, MediaFileType Type)> AddStoredImageAsync(ArtskartDbContext context)
    {
        var type = new MediaFileType { Id = 987654, MediaTypeName = "Image", MimeType = "image/png" };
        var media = new MediaFile
        {
            ObservationId = ObservationId, Origin = "https://example.org/observation.png", MediaFileType = type,
            Downloaded = false, Image = [137, 80, 78, 71], RightsHolder = "Photographer", License = "CC BY 4.0",
        };
        context.MediaFiles.Add(media);
        await context.SaveChangesAsync();
        return (media, type);
    }

    private static async Task RemoveStoredImageAsync(ArtskartDbContext context, MediaFile media, MediaFileType type)
    {
        context.MediaFiles.Remove(media);
        context.Set<MediaFileType>().Remove(type);
        await context.SaveChangesAsync();
    }

    private sealed class CapturingHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

using System.Net;
using System.Net.Http.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
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
            detail.Images.Should().ContainSingle(m => m.Id == media.Id && m.HasStoredImage);
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

    [Fact]
    public async Task Map_InvalidPresetIsRejectedWithoutCsrfHeader()
    {
        (await client.GetAsync($"/api/observations/{ObservationId}/maps/arbitrary")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

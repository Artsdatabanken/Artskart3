using System.Net;
using System.Text.Json;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Tests.Unit;

public class ObservationAnnotationServiceTests
{
    private static readonly Guid DatasetId = Guid.Parse("b124e1e0-4755-430f-9eab-894f25a9b59c");

    // Forkortet svar fra produksjon for urn:uuid:1562d182-... (annotering 837).
    private const string HiddenWithComment = """
        [{
          "id": 837,
          "comment": " Uten dokumentasjon og forkastes. ",
          "authorName": "Reidar Elven",
          "authorEmail": null,
          "dateCreated": "2024-09-17T17:41:49.2596549",
          "status": 1,
          "fields": [],
          "actions": { "acceptChanges": true, "flagObservations": true, "hideObservations": true },
          "observations": [{ "observationProxyId": "urn:uuid:1562d182", "nodeId": 1010 }]
        }]
        """;

    [Fact]
    public async Task GetAsync_SendsTheSamePropertiesAsTheOldDetailsView()
    {
        await using var context = await SeedAsync();
        var handler = new CapturingHandler(HiddenWithComment);

        var result = await CreateSut(handler, context).GetAsync(1, default);

        handler.RequestUri.Should().Be(new Uri("https://annotations.test/annotationapi/api/public-annotations/forObservation"));
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var properties = body.RootElement;
        properties.GetProperty("occurrenceId").GetString().Should().Be("urn:uuid:1562d182");
        properties.GetProperty("gbifDataSetId").GetGuid().Should().Be(DatasetId);
        properties.GetProperty("scientificName").GetString().Should().Be("Arabidopsis petraea");
        properties.GetProperty("stateProvince").GetString().Should().Be("Trøndelag");
        properties.GetProperty("observationCollected").GetDateTime().Should().Be(new DateTime(2020, 6, 19));
        properties.GetProperty("latitude").GetDouble().Should().Be(63.917121);
        properties.GetProperty("isPreservedSpecimen").GetBoolean().Should().BeFalse();

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Author = "Reidar Elven",
            Created = new DateTime(2024, 9, 17, 17, 41, 49).AddTicks(2596549),
            Comment = "Uten dokumentasjon og forkastes.",
            Changes = Array.Empty<object>(),
        });
    }

    [Fact]
    public async Task GetAsync_SendsEmptyStringsAndFallbacksWhenDataIsMissing()
    {
        await using var context = CreateInMemoryContext();
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Arabidopsis petraea" });
        context.Set<Observation>().Add(new Observation
        {
            Id = 1, MatchedScientificNameId = 1, BasisOfRecordId = 1, NodeId = 999,
            DateLastModified = new DateTime(2025, 1, 15),
        });
        await context.SaveChangesAsync();
        var handler = new CapturingHandler("[]");

        (await CreateSut(handler, context).GetAsync(1, default)).Should().BeEmpty();

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var properties = body.RootElement;
        properties.GetProperty("occurrenceId").GetString().Should().BeEmpty();
        properties.GetProperty("stateProvince").GetString().Should().BeEmpty();
        properties.GetProperty("gbifDataSetId").GetGuid().Should().Be(Guid.Empty);
        properties.GetProperty("observationCollected").GetDateTime().Should().Be(new DateTime(2025, 1, 15));
        properties.GetProperty("isPreservedSpecimen").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ShowsOnlyAcceptedFieldChangesWithANewValue()
    {
        await using var context = await SeedAsync();
        var handler = new CapturingHandler("""
            [
              { "id": 1, "comment": "Flyttet", "authorName": "A",
                "fields": [
                  { "name": "Longitude", "originalValue": "8.80075", "newValue": "9.10019" },
                  { "name": "Locality", "originalValue": "Surnadal", "newValue": "" },
                  { "name": "Habitat", "originalValue": "Skog", "newValue": null }
                ],
                "actions": { "acceptChanges": true }, "observations": [{ "observationProxyId": "urn:uuid:1562d182" }] },
              { "id": 2, "comment": "Forslag", "authorName": " ",
                "fields": [{ "name": "Latitude", "originalValue": "62.9", "newValue": "62.5" }],
                "actions": { "acceptChanges": false }, "observations": [{ "observationProxyId": "urn:uuid:1562d182" }] }
            ]
            """);

        var result = await CreateSut(handler, context).GetAsync(1, default);

        result.Should().HaveCount(2);
        result![0].Changes.Should().Equal(new Core.Application.DTOs.ObservationAnnotationChangeDto("Longitude", "8.80075", "9.10019"));
        result[1].Changes.Should().BeEmpty();
        result[1].Author.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_KeepsTheCommentButNoFieldChangesOnSensitiveObservations()
    {
        await using var context = await SeedAsync(sensitive: true);
        var handler = new CapturingHandler("""
            [{ "id": 1, "comment": "Flyttet", "authorName": "A",
               "fields": [{ "name": "Latitude", "originalValue": "63.9", "newValue": "63.917121" }],
               "actions": { "acceptChanges": true }, "observations": [{ "observationProxyId": "urn:uuid:1562d182" }] }]
            """);

        var result = await CreateSut(handler, context).GetAsync(1, default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Author = "A", Comment = "Flyttet", Changes = Array.Empty<object>(),
        });
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    public async Task GetAsync_ShowsSpeciesLevelRenamesOnlyForChangedOrTargetedObservations(
        bool hasAnnotations, bool targetsObservation, int expected)
    {
        await using var context = await SeedAsync(hasAnnotations);
        var observations = targetsObservation ? """[{ "observationProxyId": "urn:uuid:1562d182" }]""" : "[]";
        var handler = new CapturingHandler($$"""
            [{ "id": 3, "comment": "Ny art", "authorName": "A",
               "fields": [{ "name": "ScientificName", "originalValue": "Arabidopsis thaliana", "newValue": "Arabidopsis petraea" }],
               "actions": { "acceptChanges": true }, "observations": {{observations}} }]
            """);

        (await CreateSut(handler, context).GetAsync(1, default)).Should().HaveCount(expected);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForMissingOrDeletedObservationsWithoutCallingTheService()
    {
        await using var context = await SeedAsync();
        context.Set<Observation>().Add(new Observation { Id = 2, MatchedScientificNameId = 1, IsDeleted = true });
        await context.SaveChangesAsync();
        var handler = new CapturingHandler("[]");
        var sut = CreateSut(handler, context);

        (await sut.GetAsync(2, default)).Should().BeNull();
        (await sut.GetAsync(3, default)).Should().BeNull();
        handler.RequestUri.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ThrowsWhenTheServiceFails()
    {
        await using var context = await SeedAsync();
        var sut = CreateSut(new CapturingHandler("""{"title":"error"}""", HttpStatusCode.BadRequest), context);

        await sut.Invoking(s => s.GetAsync(1, default)).Should().ThrowAsync<HttpRequestException>();
    }

    private static async Task<ArtskartDbContext> SeedAsync(bool hasAnnotations = false, bool sensitive = false)
    {
        var context = CreateInMemoryContext();
        context.Set<ImportState>().Add(new ImportState { Id = 105, GbifDataSetId = DatasetId });
        context.Set<TaxonName>().Add(new TaxonName { Id = 1, ScientificName = "Arabidopsis petraea" });
        var location = new Location { Id = 10, NodeId = 105, TimeStamp = DateTime.UtcNow };
        location.Areas.Add(CreateArea(1, "Verdal", 1));
        location.Areas.Add(CreateArea(2, "Trøndelag", 2));
        var historical = CreateArea(3, "Nord-Trøndelag", 2);
        historical.IsCurrent = false;
        location.Areas.Add(historical);
        context.Set<Location>().Add(location);
        context.Set<Observation>().Add(new Observation
        {
            Id = 1,
            ProxyId = "urn:uuid:1562d182",
            NodeId = 105,
            MatchedScientificNameId = 1,
            DateTimeCollected = new DateTime(2020, 6, 19),
            DateLastModified = new DateTime(2024, 9, 18),
            Latitude = 63.917121,
            Longitude = 12.607523,
            LocationId = 10,
            BasisOfRecordId = 2,
            HasAnnotations = hasAnnotations,
            Sensitive = sensitive,
        });
        await context.SaveChangesAsync();
        return context;
    }

    private static Area CreateArea(int id, string name, int areaTypeId) => new()
    {
        Id = id, DocumentId = $"doc-{id}", Fid = $"fid-{id}", Name = name, AreaTypeId = areaTypeId,
        ParentFid = "parent", Bbox = "bbox", SyncDateTime = DateTime.UtcNow, TimeStamp = DateTime.UtcNow, IsCurrent = true,
    };

    private static ArtskartDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<ArtskartDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ObservationAnnotationService CreateSut(HttpMessageHandler handler, ArtskartDbContext context) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://annotations.test/annotationapi/") }, context);

    private sealed class CapturingHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}

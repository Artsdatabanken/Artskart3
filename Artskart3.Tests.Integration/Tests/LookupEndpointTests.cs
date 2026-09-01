using System.Net;
using System.Text.Json;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class LookupEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LookupEndpointTests(DatabaseFixture db)
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
    // GET /api/Lookup/Categories
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCategories_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetCategories_ResponseContentTypeIsApplicationJson()
    {
        var response = await _client.GetAsync("/api/Lookup/Categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/BasisOfRecords
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBasisOfRecords_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/BasisOfRecords");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Behaviors
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetBehaviors_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Behaviors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Institutions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetInstitutions_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Institutions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/SearchOrganizations
    // -----------------------------------------------------------------------


    [Fact]
    public async Task GetSearchOrganizations_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Organizations?search=test&maxCount=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Collections
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSearchCollections_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Collections?search=a&maxCount=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    /// <summary>
    /// Blankt søk avvises av modellvalideringen til [ApiController] med «The search
    /// field is required», FØR handlingen kjører. IsNullOrWhiteSpace-vakten inne i
    /// endepunktet er derfor uåtakelig over HTTP — den dekker bare direkte kall på
    /// metoden. Testen fester det som faktisk skjer, ikke det vakten antyder.
    /// </summary>
    [Fact]
    public async Task GetSearchCollections_WithBlankSearch_Returns400()
    {
        var response = await _client.GetAsync("/api/Lookup/Collections?search=%20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Datasets
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSearchDatasets_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/Datasets?search=a&maxCount=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetSearchDatasets_WithMaxCountOutOfRange_Returns400()
    {
        var response = await _client.GetAsync("/api/Lookup/Datasets?search=a&maxCount=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/CatalogNumbers
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSearchCatalogNumbers_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/CatalogNumbers?search=10&maxCount=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    /// <summary>
    /// Minstelengden håndheves på serveren, ikke bare i typeaheaden: endepunktet
    /// kan kalles direkte, og et prefikssøk på ett tegn ville skannet 61M rader.
    /// </summary>
    [Fact]
    public async Task GetSearchCatalogNumbers_WithSearchShorterThanMinimum_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/Lookup/CatalogNumbers?search=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Eksakt oppslag paa ProxyId / OccurrenceId
    //
    // Artskart 2 soekte paa katalognummer, ProxyId og OccurrenceId under ett, med
    // LIKE '%x%'. Maalt paa produksjonslik kopi koster det 12,2 s og 29,0 s per
    // soek, saa de to URN-kolonnene matches eksakt i stedet. Prefiks er ikke et
    // alternativ: katalognummeret ligger til SLUTT i dem
    // (biofokus/biofokus/104168), saa 'x%' ville aldri truffet.
    // -----------------------------------------------------------------------

    private const string SeededProxyId = "biofokus/biofokus/104168";
    private const int SeededObservationId = 8368071;

    [Fact]
    public async Task GetSearchCatalogNumbers_WithExactProxyId_ReturnsThatObservation()
    {
        var response = await _client.GetAsync(
            $"/api/Lookup/CatalogNumbers?search={Uri.EscapeDataString(SeededProxyId)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var ider = doc.RootElement.EnumerateArray()
            .SelectMany(m => m.GetProperty("observationIds").EnumerateArray())
            .Select(id => id.GetInt32())
            .ToList();

        ider.Should().Contain(SeededObservationId,
            "et innlimt ProxyId skal finne observasjonen sin");
    }

    /// <summary>
    /// Sorteringen i databasen er case-insensitiv, og ProxyId og OccurrenceId er
    /// samme verdi med ulik bokstavstoerrelse. Kopierer brukeren fra en kilde som
    /// versaliserer, skal treffet vaere det samme.
    /// </summary>
    [Fact]
    public async Task GetSearchCatalogNumbers_WithExactProxyIdInUpperCase_ReturnsSameObservation()
    {
        var response = await _client.GetAsync(
            $"/api/Lookup/CatalogNumbers?search={Uri.EscapeDataString(SeededProxyId.ToUpperInvariant())}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var ider = doc.RootElement.EnumerateArray()
            .SelectMany(m => m.GetProperty("observationIds").EnumerateArray())
            .Select(id => id.GetInt32())
            .ToList();

        ider.Should().Contain(SeededObservationId);
    }

    /// <summary>
    /// Bare EKSAKT treff. Et halvt ProxyId skal ikke gi noe - ellers er vi tilbake
    /// til delstrengsoeket som kostet 12-29 sekunder.
    /// </summary>
    [Fact]
    public async Task GetSearchCatalogNumbers_WithPartialProxyId_ReturnsNothingForIt()
    {
        var delvis = SeededProxyId[..12];

        var response = await _client.GetAsync(
            $"/api/Lookup/CatalogNumbers?search={Uri.EscapeDataString(delvis)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var ider = doc.RootElement.EnumerateArray()
            .SelectMany(m => m.GetProperty("observationIds").EnumerateArray())
            .Select(id => id.GetInt32())
            .ToList();

        ider.Should().NotContain(SeededObservationId,
            "delvis ProxyId er ikke stoettet - eksakt treff er hele poenget");
    }

    /// <summary>
    /// Katalognummersoeket skal fortsatt virke som prefikssoek. Regresjonsvakt for
    /// at det eksakte oppslaget ikke fortrenger den opprinnelige oppfoerselen.
    /// </summary>
    [Fact]
    public async Task GetSearchCatalogNumbers_WithCatalogNumberPrefix_StillReturnsMatches()
    {
        var response = await _client.GetAsync("/api/Lookup/CatalogNumbers?search=10416");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0,
            "prefikssoek paa katalognummer er den opprinnelige funksjonen");
    }

    [Fact]
    public async Task GetSearchCatalogNumbers_WithUnknownIdentifier_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync(
            "/api/Lookup/CatalogNumbers?search=finnes-helt-sikkert-ikke-42");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/Areas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAreas_Returns200WithJsonObject()
    {
        var response = await _client.GetAsync("/api/Lookup/Areas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/TaxonGroups
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTaxonGroups_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonGroups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/TaxonTree
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTaxonTree_WithoutParent_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonTree");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetTaxonTree_WithParentTaxonId_Returns200WithJsonArray()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonTree?parentTaxonId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // -----------------------------------------------------------------------
    // GET /api/Lookup/TaxonAncestry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTaxonAncestry_WithoutIds_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonAncestry");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetTaxonAncestry_WithIds_Returns200WithOneEntryPerDistinctId()
    {
        var response = await _client.GetAsync("/api/Lookup/TaxonAncestry?taxonIds=1&taxonIds=2&taxonIds=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(2);

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            entry.TryGetProperty("id", out _).Should().BeTrue();
            entry.TryGetProperty("parentIds", out var parentIds).Should().BeTrue();
            parentIds.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Constants;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class SavedFilterEndpointTests : IAsyncLifetime
{
    private const string BaseUrl = "/api/User/SavedFilters";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SavedFilterEndpointTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(db.ConnectionString, useTestAuthentication: true);
        // Ny bruker per testklasse-instans, så testene ikke ser hverandres filtre.
        _client = CreateClient(Guid.NewGuid());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // GET /api/User/SavedFilters
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSavedFilters_ForNewUser_ReturnsEmptyArray()
    {
        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);

        filters.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSavedFilters_DoesNotLeakOtherUsersFilters()
    {
        using var otherClient = CreateClient(Guid.NewGuid());
        await CreateAsync(_client, "mitt-filter");

        var filters = await otherClient.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);

        filters.Should().NotContain(f => f.Name == "mitt-filter");
    }

    [Fact]
    public async Task GetSavedFilters_WithoutIdentity_Returns401()
    {
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-CSRF", "1");

        var response = await anonymous.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // POST /api/User/SavedFilters
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateSavedFilter_WithValidRequest_IsListedWithFilterAndExtent()
    {
        var request = Request("Pattedyr i Trøndelag");
        request.Filter = new ObservationSearchFilterDto
        {
            TaxonGroupIds = [12],
            CountyIds = ["50"],
            Period = new PeriodDto { From = 1980, Months = [5, 6] },
            WithImages = true,
            PageNumber = 2,
            ResultsPerPage = 50,
        };
        request.Extent = new MapExtentDto { MinX = 100000, MinY = 6900000, MaxX = 400000, MaxY = 7200000 };

        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<SavedFilterDto>();
        var listed = (await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl))!.Single();
        listed.Id.Should().Be(created!.Id);
        listed.Name.Should().Be("Pattedyr i Trøndelag");
        listed.Filter.TaxonGroupIds.Should().Equal(12);
        listed.Filter.CountyIds.Should().Equal("50");
        listed.Filter.Period!.Months.Should().Equal(5, 6);
        listed.Filter.WithImages.Should().BeTrue();
        listed.Filter.PageNumber.Should().BeNull();
        listed.Extent.Should().BeEquivalentTo(request.Extent);
    }

    [Fact]
    public async Task CreateSavedFilter_WithoutIdentity_Returns401()
    {
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-CSRF", "1");

        var response = await anonymous.PostAsJsonAsync(BaseUrl, Request("anonym"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSavedFilter_WithTooLongName_Returns400()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, Request(new string('x', 201)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedFilter_WithBlankName_Returns400()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, Request("   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedFilter_WithInvertedExtent_Returns400()
    {
        var request = Request("Feil utsnitt");
        request.Extent = new MapExtentDto { MinX = 10, MinY = 10, MaxX = 0, MaxY = 0 };

        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedFilter_WithFilterLargerThanSearchAllows_Returns400()
    {
        var request = Request("For stort");
        request.Filter = new ObservationSearchFilterDto
        {
            TaxonIds = Enumerable.Range(1, SearchConstants.MaxFilterArraySize + 1).ToArray(),
        };

        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedFilter_WithInvertedCoordinatePrecision_Returns400()
    {
        var request = Request("Feil presisjon");
        request.Filter = new ObservationSearchFilterDto { CoordinatePrecision = new CoordinatePrecisionDto { From = 500, To = 100 } };

        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedFilter_AsDefault_ClearsPreviousDefault()
    {
        var first = await CreateAsync(_client, "først", isDefault: true);
        var second = await CreateAsync(_client, "andre", isDefault: true);

        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);

        filters!.Single(f => f.Id == first.Id).IsDefault.Should().BeFalse();
        filters!.Single(f => f.Id == second.Id).IsDefault.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // DELETE /api/User/SavedFilters/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteSavedFilter_OwnFilter_Returns204AndRemovesIt()
    {
        var created = await CreateAsync(_client, "slettes");

        var response = await _client.DeleteAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);
        filters.Should().NotContain(f => f.Id == created.Id);
    }

    [Fact]
    public async Task DeleteSavedFilter_Twice_Returns404()
    {
        var created = await CreateAsync(_client, "slettes to ganger");
        await _client.DeleteAsync($"{BaseUrl}/{created.Id}");

        var response = await _client.DeleteAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSavedFilter_OtherUsersFilter_Returns404AndKeepsIt()
    {
        using var otherClient = CreateClient(Guid.NewGuid());
        var created = await CreateAsync(_client, "beholdes");

        var response = await otherClient.DeleteAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);
        filters.Should().Contain(f => f.Id == created.Id);
    }

    // -----------------------------------------------------------------------
    // PUT/DELETE /api/User/SavedFilters/{id}/default
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetDefaultSavedFilter_SwitchesDefault()
    {
        var first = await CreateAsync(_client, "først", isDefault: true);
        var second = await CreateAsync(_client, "andre");

        var response = await _client.PutAsync($"{BaseUrl}/{second.Id}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);
        filters!.Single(f => f.Id == first.Id).IsDefault.Should().BeFalse();
        filters!.Single(f => f.Id == second.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultSavedFilter_OtherUsersFilter_Returns404()
    {
        using var otherClient = CreateClient(Guid.NewGuid());
        var created = await CreateAsync(_client, "ikke ditt");

        var response = await otherClient.PutAsync($"{BaseUrl}/{created.Id}/default", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClearDefaultSavedFilter_UnsetsDefault()
    {
        var created = await CreateAsync(_client, "standard", isDefault: true);

        var response = await _client.DeleteAsync($"{BaseUrl}/{created.Id}/default");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var filters = await _client.GetFromJsonAsync<List<SavedFilterDto>>(BaseUrl);
        filters!.Single(f => f.Id == created.Id).IsDefault.Should().BeFalse();
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static CreateSavedFilterRequestDto Request(string name, bool isDefault = false) => new()
    {
        Name = name,
        Filter = new ObservationSearchFilterDto { TaxonGroupIds = [1] },
        IsDefault = isDefault,
    };

    private static async Task<SavedFilterDto> CreateAsync(HttpClient client, string name, bool isDefault = false)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, Request(name, isDefault));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SavedFilterDto>())!;
    }
}

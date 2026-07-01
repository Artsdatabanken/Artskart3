using System.Net;
using System.Text.Json;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Services.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Unit;

public class SpeciesServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IOptions<NorTaxaOptions> DefaultOptions = Options.Create(new NorTaxaOptions());

    private static SpeciesService CreateSut(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://nortaxa.test/") };
        return new SpeciesService(client, DefaultOptions);
    }

    // -----------------------------------------------------------------------
    // Navnesøk (ikke-heltall input)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpeciesAsync_WithStringInput_CallsSearchEndpoint()
    {
        var searchResults = new List<NorTaxaSearchResult>
        {
            new()
            {
                TaxonId = 79773,
                AcceptedScientificName = new NorTaxaAcceptedScientificName
                {
                    Name = "Margaritifera margaritifera",
                    NameFormatted = "<i>Margaritifera margaritifera</i>",
                    Author = "(Linnaeus, 1758)",
                    Rank = "Species"
                },
                PreferredVernacularNames = [new NorTaxaVernacularName { Name = "elvemusling", NameLanguageIso = "nb" }],
                ScientificNameSynonyms = [],
                VernacularNameSynonyms = [new NorTaxaVernacularName { Name = "elveskjel", NameLanguageIso = "nn" }]
            }
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(searchResults, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("elvemusling");

        result.Should().HaveCount(1);
        result[0].TaxonId.Should().Be(79773);
        result[0].ScientificName.Should().Be("Margaritifera margaritifera");
        result[0].Author.Should().Be("(Linnaeus, 1758)");
        result[0].Rank.Should().Be("Species");
        result[0].PreferredVernacularNames.Should().ContainSingle(v => v.Name == "elvemusling" && v.Language == "nb");
        result[0].VernacularNameSynonyms.Should().ContainSingle(v => v.Name == "elveskjel" && v.Language == "nn");
        handler.LastRequestUri!.PathAndQuery.Should().Contain("Search?Search=elvemusling&MaxResults=20");
    }

    [Fact]
    public async Task SearchSpeciesAsync_WithStringInput_MapsScientificNameSynonyms()
    {
        var searchResults = new List<NorTaxaSearchResult>
        {
            new()
            {
                TaxonId = 100322,
                AcceptedScientificName = new NorTaxaAcceptedScientificName
                {
                    Name = "Pholeoixodes hexagonus",
                    NameFormatted = "<i>Pholeoixodes hexagonus</i>",
                    Author = "Leach, 1815",
                    Rank = "Species"
                },
                PreferredVernacularNames = [],
                ScientificNameSynonyms =
                [
                    new NorTaxaScientificNameSynonym
                    {
                        Name = "Ixodes hexagonus",
                        NameFormatted = "<i>Ixodes hexagonus</i>",
                        Author = "Leach, 1815",
                        Rank = "Species"
                    }
                ],
                VernacularNameSynonyms = []
            }
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(searchResults, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("flått");

        result[0].ScientificNameSynonyms.Should().ContainSingle(s => s.Name == "Ixodes hexagonus");
    }

    [Fact]
    public async Task SearchSpeciesAsync_WithStringInput_UrlEncodesSearchTerm()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        await sut.SearchSpeciesAsync("flått med mellomrom");

        handler.LastRequestUri!.PathAndQuery.Should().Contain("Search=fl%C3%A5tt%20med%20mellomrom");
    }

    // -----------------------------------------------------------------------
    // TaxonId-oppslag (heltall input)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpeciesAsync_WithIntegerInput_CallsByTaxonIdEndpoint()
    {
        var byIdResult = new NorTaxaByTaxonIdResult
        {
            TaxonId = 79773,
            ScientificNames =
            [
                new NorTaxaScientificName
                {
                    TaxonomicStatus = "Accepted",
                    ScientificNamePresentation = "Margaritifera margaritifera",
                    ScientificNamePresentationHtml = "<i>Margaritifera margaritifera</i>",
                    ScientificNameAuthorship = "(Linnaeus, 1758)",
                    TaxonRank = "Species"
                }
            ],
            VernacularNames =
            [
                new NorTaxaDetailedVernacularName
                {
                    VernacularName = "elvemusling",
                    VernacularNameStatus = "Recommended",
                    LanguageIsoCode = "nb"
                },
                new NorTaxaDetailedVernacularName
                {
                    VernacularName = "elveskjel",
                    VernacularNameStatus = "NotRecommended",
                    LanguageIsoCode = "nn"
                }
            ]
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(byIdResult, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("79773");

        result.Should().HaveCount(1);
        result[0].TaxonId.Should().Be(79773);
        result[0].ScientificName.Should().Be("Margaritifera margaritifera");
        result[0].PreferredVernacularNames.Should().ContainSingle(v => v.Name == "elvemusling");
        result[0].VernacularNameSynonyms.Should().ContainSingle(v => v.Name == "elveskjel");
        handler.LastRequestUri!.PathAndQuery.Should().Contain("ByTaxonId/79773");
    }

    [Fact]
    public async Task SearchSpeciesAsync_WithIntegerInput_MapsScientificNameSynonymsFromByTaxonId()
    {
        var byIdResult = new NorTaxaByTaxonIdResult
        {
            TaxonId = 100322,
            ScientificNames =
            [
                new NorTaxaScientificName
                {
                    TaxonomicStatus = "Accepted",
                    ScientificNamePresentation = "Pholeoixodes hexagonus",
                    ScientificNamePresentationHtml = "<i>Pholeoixodes hexagonus</i>",
                    ScientificNameAuthorship = "Leach, 1815",
                    TaxonRank = "Species"
                },
                new NorTaxaScientificName
                {
                    TaxonomicStatus = "Synonym",
                    ScientificNamePresentation = "Ixodes hexagonus",
                    ScientificNamePresentationHtml = "<i>Ixodes hexagonus</i>",
                    ScientificNameAuthorship = "Leach, 1815",
                    TaxonRank = "Species"
                }
            ],
            VernacularNames = []
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(byIdResult, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("100322");

        result[0].ScientificNameSynonyms.Should().ContainSingle(s => s.Name == "Ixodes hexagonus" && s.Author == "Leach, 1815");
    }

    // -----------------------------------------------------------------------
    // Feilhåndtering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchSpeciesAsync_WhenByTaxonIdHasNoAcceptedName_ReturnsFallbackDto()
    {
        var byIdResult = new NorTaxaByTaxonIdResult
        {
            TaxonId = 12345,
            ScientificNames =
            [
                new NorTaxaScientificName
                {
                    TaxonomicStatus = "Synonym",
                    ScientificNamePresentation = "Old name",
                    ScientificNamePresentationHtml = "<i>Old name</i>",
                    ScientificNameAuthorship = "Author",
                    TaxonRank = "Species"
                }
            ],
            VernacularNames = []
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(byIdResult, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("12345");

        result.Should().HaveCount(1);
        result[0].TaxonId.Should().Be(12345);
        result[0].ScientificName.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenSearchReturnsServerError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var sut = CreateSut(handler);

        var act = () => sut.SearchSpeciesAsync("elvemusling");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenByTaxonIdReturnsServerError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var sut = CreateSut(handler);

        var act = () => sut.SearchSpeciesAsync("79773");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WithNegativeNumber_TreatsAsNameSearch()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        await sut.SearchSpeciesAsync("-5");

        handler.LastRequestUri!.PathAndQuery.Should().Contain("Search?Search=-5");
    }

    [Fact]
    public async Task SearchSpeciesAsync_WithZero_TreatsAsNameSearch()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        await sut.SearchSpeciesAsync("0");

        handler.LastRequestUri!.PathAndQuery.Should().Contain("Search?Search=0");
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenByTaxonIdReturns404_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "");
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("999999");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenSearchReturnsEmptyArray_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var sut = CreateSut(handler);

        var result = await sut.SearchSpeciesAsync("finnesikke");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenConnectionFails_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        var act = () => sut.SearchSpeciesAsync("elvemusling");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchSpeciesAsync_WhenTimeout_ThrowsTaskCanceledException()
    {
        var handler = new FakeHttpMessageHandler(new TaskCanceledException("Timeout", new TimeoutException()));
        var sut = CreateSut(handler);

        var act = () => sut.SearchSpeciesAsync("elvemusling");

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    // -----------------------------------------------------------------------
    // Fake HttpMessageHandler
    // -----------------------------------------------------------------------

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly string? _responseContent;
        private readonly Exception? _exception;

        public Uri? LastRequestUri { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        public FakeHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (_exception != null)
                throw _exception;

            return Task.FromResult(new HttpResponseMessage(_statusCode!.Value)
            {
                Content = new StringContent(_responseContent!, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

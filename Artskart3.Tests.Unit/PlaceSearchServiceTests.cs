using System.Net;
using System.Text.Json;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Services.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Unit;

public class PlaceSearchServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IOptions<GeonorgeOptions> DefaultOptions = Options.Create(new GeonorgeOptions());

    private static PlaceSearchService CreateSut(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://geonorge.test/") };
        return new PlaceSearchService(client, DefaultOptions);
    }

    [Fact]
    public async Task SearchPlacesAsync_WithValidSearch_MapsResultsCorrectly()
    {
        var response = new GeonorgeStedResponse
        {
            Navn =
            [
                new GeonorgeSted
                {
                    Stedsnummer = 307915,
                    Stedstatus = "aktiv",
                    Navneobjekttype = "By",
                    Representasjonspunkt = new GeonorgeRepresentasjonspunkt { Ost = 261000, Nord = 6649000, Koordsys = 25833 },
                    Stedsnavn =
                    [
                        new GeonorgeSkrivemate { Skrivemate = "Oslo", Navnestatus = "hovednavn", Sprak = "Norsk" },
                        new GeonorgeSkrivemate { Skrivemate = "Oslove", Navnestatus = "sidenavn", Sprak = "Sørsamisk" }
                    ],
                    Kommuner = [new GeonorgeKommune { Kommunenavn = "Oslo", Kommunenummer = "0301" }],
                    Fylker = [new GeonorgeFylke { Fylkesnavn = "Oslo", Fylkesnummer = "03" }]
                }
            ]
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(response, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchPlacesAsync("Oslo");

        result.Should().HaveCount(1);
        result[0].StedsNummer.Should().Be(307915);
        result[0].Name.Should().Be("Oslo");
        result[0].NavneObjektType.Should().Be("By");
        result[0].RecommendedZoom.Should().Be(3);
        result[0].East.Should().Be(261000);
        result[0].North.Should().Be(6649000);
        result[0].CoordinateSystem.Should().Be(25833);
        result[0].Municipalities.Should().ContainSingle("Oslo");
        result[0].Counties.Should().ContainSingle("Oslo");
        result[0].AlternativeNames.Should().ContainSingle(a => a.Name == "Oslove" && a.Language == "Sørsamisk");
        handler.LastRequestUri!.PathAndQuery.Should().Contain("sted?sok=Oslo");
    }

    [Fact]
    public async Task SearchPlacesAsync_WithEmptySearch_ReturnsEmptyListWithoutCallingGeonorge()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler);

        var result = await sut.SearchPlacesAsync("   ");

        result.Should().BeEmpty();
        handler.LastRequestUri.Should().BeNull();
    }

    [Fact]
    public async Task SearchPlacesAsync_WhenNotFound_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty);
        var sut = CreateSut(handler);

        var result = await sut.SearchPlacesAsync("finnesikke");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchPlacesAsync_WhenGeonorgeReturnsError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty);
        var sut = CreateSut(handler);

        var act = () => sut.SearchPlacesAsync("Oslo");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchPlacesAsync_WithUnknownNavneobjekttype_FallsBackToDefaultZoom()
    {
        var response = new GeonorgeStedResponse
        {
            Navn =
            [
                new GeonorgeSted
                {
                    Stedsnummer = 42,
                    Navneobjekttype = "UkjentType",
                    Representasjonspunkt = new GeonorgeRepresentasjonspunkt { Ost = 1, Nord = 2, Koordsys = 25833 },
                    Stedsnavn = [new GeonorgeSkrivemate { Skrivemate = "Ukjent", Navnestatus = "hovednavn" }]
                }
            ]
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(response, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchPlacesAsync("Ukjent");

        result.Should().ContainSingle();
        result[0].RecommendedZoom.Should().Be(2);
    }

    [Fact]
    public async Task SearchPlacesAsync_WhenNoStedsnavnHasHovednavn_FallsBackToFirstName()
    {
        var response = new GeonorgeStedResponse
        {
            Navn =
            [
                new GeonorgeSted
                {
                    Stedsnummer = 1,
                    Navneobjekttype = "Fjell",
                    Representasjonspunkt = new GeonorgeRepresentasjonspunkt { Ost = 1, Nord = 2, Koordsys = 25833 },
                    Stedsnavn = [new GeonorgeSkrivemate { Skrivemate = "Sidenavn", Navnestatus = "sidenavn" }]
                }
            ]
        };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(response, JsonOptions));
        var sut = CreateSut(handler);

        var result = await sut.SearchPlacesAsync("Sidenavn");

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Sidenavn");
    }

    // -----------------------------------------------------------------------
    // Fake HttpMessageHandler
    // -----------------------------------------------------------------------

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public Uri? LastRequestUri { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

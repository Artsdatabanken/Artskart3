using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vokter det ferdig serialiserte områdesvaret.
///
/// Serialiseringen skjer i SearchService i stedet for i MVC, og da er det ingenting som
/// automatisk holder formatet i takt med resten av API-et. Endrer noen JSON-oppsettet i
/// Program.cs — eller navnepolicyen — ville dette ene endepunktet stille begynt å svare i
/// et annet format enn alle andre. Frontend leser <c>observationCount</c> og <c>fid</c>;
/// PascalCase ville gitt tomme kart uten en eneste feilmelding.
/// </summary>
public class AreaMarkersPayloadMatcherTests
{
    private readonly Mock<ISearchRepository> _repository = new();
    private readonly SearchService _sut;

    private static readonly AreaMarkerDto[] Markers =
    [
        new() { Id = 1, Fid = "0301", Name = "Oslo", AreaTypeId = 1, ObservationCount = 42, ParentFid = "03" },
        new() { Id = 2, Fid = "4601", Name = "Bergen", AreaTypeId = 1, ObservationCount = 7, ParentFid = "46" },
    ];

    public AreaMarkersPayloadMatcherTests()
    {
        _repository
            .Setup(r => r.GetAreaMarkersAsync(It.IsAny<int>(), It.IsAny<LocationSearchFilterDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Markers);

        _sut = new SearchService(_repository.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// JsonSerializerDefaults.Web er det MVC bruker. Speiler ikke tjenesten det, får
    /// klienten PascalCase fra dette endepunktet og camelCase fra alle andre.
    /// </summary>
    [Fact]
    public async Task Ferdig_serialisert_svar_er_identisk_med_rammeverkets()
    {
        var payload = await _sut.GetAreaMarkersPayloadAsync(2);

        var forventet = JsonSerializer.SerializeToUtf8Bytes(Markers, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        });

        payload.Raw.Should().Equal(forventet);
    }

    [Fact]
    public async Task Noeklene_er_camelCase()
    {
        var json = Encoding.UTF8.GetString((await _sut.GetAreaMarkersPayloadAsync(2)).Raw);

        json.Should().Contain("\"observationCount\":").And.Contain("\"fid\":");
        json.Should().NotContain("\"ObservationCount\":");
    }

    [Fact]
    public async Task Gzip_pakker_ut_til_samme_bytes()
    {
        var payload = await _sut.GetAreaMarkersPayloadAsync(2);

        using var inn = new MemoryStream(payload.Gzip);
        using var gzip = new GZipStream(inn, CompressionMode.Decompress);
        using var ut = new MemoryStream();
        await gzip.CopyToAsync(ut);

        ut.ToArray().Should().Equal(payload.Raw);
    }

    [Fact]
    public async Task Svaret_bufres_saa_repositoriet_bare_spoerres_en_gang()
    {
        // Hele poenget: to kall per sidelast skal ikke koste to serialiseringer.
        await _sut.GetAreaMarkersPayloadAsync(2);
        await _sut.GetAreaMarkersPayloadAsync(2);

        _repository.Verify(
            r => r.GetAreaMarkersAsync(2, It.IsAny<LocationSearchFilterDto?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Zoomnivaaene_bufres_hver_for_seg()
    {
        var z1 = await _sut.GetAreaMarkersPayloadAsync(1);
        var z2 = await _sut.GetAreaMarkersPayloadAsync(2);

        z1.Should().NotBeSameAs(z2);
        _repository.Verify(
            r => r.GetAreaMarkersAsync(It.IsAny<int>(), It.IsAny<LocationSearchFilterDto?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}

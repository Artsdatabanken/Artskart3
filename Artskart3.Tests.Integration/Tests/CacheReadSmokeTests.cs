using Artskart3.Core.Application.Import;
using Artskart3.Infrastructure.Cache;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Leser mot en ekte Artskart 2-cache og viser at lesestien virker: blober pakkes ut,
/// JSON-en går inn i <c>CacheRecord</c>, og norske tegn kommer gjennom.
///
/// AVSLÅTT SOM STANDARD. Testen krever en cache-database, som bare finnes på
/// utviklermaskiner og på riggen. Uten miljøvariabelen gjør den ingenting:
///
///   $env:ARTSKART_CACHE_CONNECTION = "data source=localhost;initial catalog=Artskart2Cache-2026-9-22-14-36;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~CacheReadSmoke"
///
/// Valgfritt: ARTSKART_CACHE_NODE (standard 50) og ARTSKART_CACHE_MAXRECORDS
/// (standard 500).
///
/// BRUKER IKKE DatabaseFixture, og trenger derfor ikke Docker. Den leser bare — ingen
/// skriving, ingen migrering.
/// </summary>
public class CacheReadSmokeTests
{
    private const string ConnectionVariable = "ARTSKART_CACHE_CONNECTION";

    private readonly ITestOutputHelper _output;

    public CacheReadSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

    private static int NodeId =>
        int.TryParse(Environment.GetEnvironmentVariable("ARTSKART_CACHE_NODE"), out var node) ? node : 50;

    private static int MaxRecords =>
        int.TryParse(Environment.GetEnvironmentVariable("ARTSKART_CACHE_MAXRECORDS"), out var maks) ? maks : 500;

    private static ArtskartCacheDbContext NyKontekst(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ArtskartCacheDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(600))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ArtskartCacheDbContext(options);
    }

    [Fact]
    public async Task Leser_poster_fra_ekte_cache()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await using var context = NyKontekst(connectionString);
        var leser = new CacheRecordReader(context, NullLogger<CacheRecordReader>.Instance);

        var statistikk = await leser.GetNodeStatisticsAsync(NodeId, CacheEntityStatus.All, CancellationToken.None);

        _output.WriteLine(
            $"Node {statistikk.NodeId} ({statistikk.NodeName ?? "ukjent navn"}): {statistikk.RecordCount:N0} rader, " +
            $"siste harvest {statistikk.MaxTrackInfoDateImported:u}, sensitiv: {statistikk.IsSensitive}, " +
            $"harvests eget tall: {statistikk.NumberOfRecordsInCache:N0}");

        statistikk.RecordCount.Should().BeGreaterThan(0,
            $"node {NodeId} må finnes i cache-kopien - sett ARTSKART_CACHE_NODE til en node som gjør det");

        var options = new CacheReadOptions { MaxRecords = MaxRecords, BatchSize = 100 };

        var lest = 0;
        var ødelagte = 0;
        var medArtsnavn = 0;
        var medKoordinat = 0;
        var medGeometri = 0;
        var medUkjenteFelt = 0;
        string? førsteArtsnavn = null;

        await foreach (var item in leser.ReadNodeAsync(NodeId, options, CancellationToken.None))
        {
            lest++;

            if (item.Record is null)
            {
                ødelagte++;
                _output.WriteLine($"ULESBAR rad {item.CacheRowId} ({item.ProxyId}): {item.Error}");
                continue;
            }

            var record = item.Record;

            item.ProxyId.Should().NotBeNullOrWhiteSpace();
            record.NodeDatabaseID.Should().Be(NodeId, "blobben skal tilhøre noden vi spurte om");
            record.HashCode.Should().NotBe(0, "Hash leses fra kolonnen, ikke fra blobben");
            record.CacheRowId.Should().Be(item.CacheRowId);

            if (!string.IsNullOrWhiteSpace(record.ScientificName))
            {
                medArtsnavn++;
                førsteArtsnavn ??= record.ScientificName;
            }

            if (record.Latitude != 0 || record.Longitude != 0) medKoordinat++;
            if (!string.IsNullOrWhiteSpace(record.FootprintWkt)) medGeometri++;
            if (record.UnknownFields is { Count: > 0 }) medUkjenteFelt++;
        }

        _output.WriteLine(
            $"Lest {lest} poster: {ødelagte} ulesbare, {medArtsnavn} med artsnavn, {medKoordinat} med koordinat, " +
            $"{medGeometri} med footprint-geometri, {medUkjenteFelt} med ukjente felt. Første artsnavn: {førsteArtsnavn}");

        lest.Should().BeGreaterThan(0);
        ødelagte.Should().Be(0, "en ødelagt blob her betyr at dekomprimeringen eller JSON-laget er feil portert");
        medArtsnavn.Should().BeGreaterThan(0, "en node uten et eneste artsnavn ville betydd at feltnavnene ikke treffer");
    }

    /// <summary>
    /// Dekningsmålingen fra plan 4.8.3: hvilke noder finnes i cache-kopien, og med hvor
    /// mange rader. Tallene sier hvor mye av produksjonscachen pariteten faktisk kan
    /// bekreftes for — og hvilken node en lesetest skal peke på.
    ///
    /// Én GROUP BY over hele tabellen. Minutter på en produksjonscache; sekunder på en
    /// nedlastet kopi.
    /// </summary>
    [Fact]
    public async Task Lister_nodene_i_cache_kopien()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await using var context = NyKontekst(connectionString);
        var leser = new CacheRecordReader(context, NullLogger<CacheRecordReader>.Instance);

        var noder = await leser.GetNodesAsync(CancellationToken.None);

        _output.WriteLine($"Noder med rader i cache-kopien: {noder.Count}");
        _output.WriteLine($"Rader totalt: {noder.Sum(x => (long)x.RecordCount):N0}");

        foreach (var (nodeId, antall) in noder.OrderByDescending(x => x.RecordCount))
        {
            _output.WriteLine($"  node {nodeId,5}: {antall,12:N0} rader");
        }

        noder.Should().NotBeEmpty("cache-kopien skal ha rader");
    }

    [Fact]
    public async Task Vannmerket_gir_bare_nyere_poster()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await using var context = NyKontekst(connectionString);
        var leser = new CacheRecordReader(context, NullLogger<CacheRecordReader>.Instance);

        var førstePoster = await leser
            .ReadNodeAsync(NodeId, new CacheReadOptions { MaxRecords = 50, BatchSize = 50 }, CancellationToken.None)
            .ToListAsync(CancellationToken.None);

        førstePoster.Should().NotBeEmpty();

        var vannmerke = førstePoster
            .Where(x => x.Record is not null)
            .Max(x => x.Record!.TrackInfoDateImported);

        var etterVannmerke = await leser
            .ReadNodeAsync(
                NodeId,
                new CacheReadOptions { ChangedAfter = vannmerke, MaxRecords = 50, BatchSize = 50 },
                CancellationToken.None)
            .ToListAsync(CancellationToken.None);

        _output.WriteLine($"Vannmerke {vannmerke:u} ga {etterVannmerke.Count} poster.");

        etterVannmerke
            .Where(x => x.Record is not null)
            .Should().OnlyContain(x => x.Record!.TrackInfoDateImported > vannmerke);
    }
}

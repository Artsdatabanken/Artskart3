using System.IO.Compression;
using System.Text;
using Artskart3.Core.Application.Import;
using Artskart3.Infrastructure.Cache;
using Artskart3.Infrastructure.Cache.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

public class CacheRecordReaderTests
{
    private static readonly DateTime Basis = new(2024, 1, 1, 12, 0, 0);

    private static byte[] Blob(string json)
    {
        var bytes = Encoding.Unicode.GetBytes(json);
        using var ut = new MemoryStream();
        using (var gzip = new GZipStream(ut, CompressionMode.Compress))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return ut.ToArray();
    }

    private static CacheDarwinCoreProxy Rad(
        int id,
        int nodeId,
        bool isDeleted = false,
        DateTime? trackDate = null,
        string? json = null,
        byte[]? blob = null)
    {
        var proxyId = $"p_{id}";

        return new CacheDarwinCoreProxy
        {
            Id = id,
            ProxyId = proxyId,
            NodeId = nodeId,
            IsDeleted = isDeleted,
            TrackInfoDateImported = trackDate ?? Basis.AddMinutes(id),
            DateLastModified = Basis,
            Hash = 1000 + id,
            SerializedObject = blob ?? Blob(json ?? $"{{\"Id\":\"{proxyId}\",\"NodeDatabaseID\":{nodeId},\"IsDeleted\":{(isDeleted ? "true" : "false")},\"ScientificName\":\"Vulpes vulpes\"}}")
        };
    }

    private static ArtskartCacheDbContext NyKontekst(params CacheDarwinCoreProxy[] rader)
    {
        var options = new DbContextOptionsBuilder<ArtskartCacheDbContext>()
            .UseInMemoryDatabase($"cache_{Guid.NewGuid()}")
            .Options;

        var context = new ArtskartCacheDbContext(options);
        context.DarwinCoreProxies.AddRange(rader);
        context.SaveChanges();

        return context;
    }

    private static CacheRecordReader NyLeser(ArtskartCacheDbContext context) =>
        new(context, NullLogger<CacheRecordReader>.Instance);

    [Fact]
    public async Task ReadNodeAsync_leser_bare_noden_det_spoerres_om()
    {
        using var context = NyKontekst(Rad(1, 50), Rad(2, 50), Rad(3, 18));
        var leser = NyLeser(context);

        var poster = await leser.ReadNodeAsync(50, new CacheReadOptions(), CancellationToken.None).ToListAsync();

        poster.Should().HaveCount(2);
        poster.Should().OnlyContain(x => x.Record!.NodeDatabaseID == 50);
    }

    [Fact]
    public async Task ReadNodeAsync_setter_hash_og_radid_fra_kolonnene()
    {
        using var context = NyKontekst(Rad(7, 50));
        var leser = NyLeser(context);

        var post = await leser.ReadNodeAsync(50, new CacheReadOptions(), CancellationToken.None).FirstAsync();

        post.CacheRowId.Should().Be(7);
        post.ProxyId.Should().Be("p_7");
        post.Record!.HashCode.Should().Be(1007);
        post.Record.CacheRowId.Should().Be(7);
    }

    [Fact]
    public async Task ReadNodeAsync_leser_alle_rader_naar_de_spenner_over_flere_batcher()
    {
        var rader = Enumerable.Range(1, 25).Select(i => Rad(i, 50)).ToArray();
        using var context = NyKontekst(rader);
        var leser = NyLeser(context);

        var poster = await leser
            .ReadNodeAsync(50, new CacheReadOptions { BatchSize = 10 }, CancellationToken.None)
            .ToListAsync();

        poster.Should().HaveCount(25);
        poster.Select(x => x.CacheRowId).Should().BeInAscendingOrder();
        poster.Select(x => x.CacheRowId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ReadNodeAsync_stopper_paa_MaxRecords()
    {
        var rader = Enumerable.Range(1, 25).Select(i => Rad(i, 50)).ToArray();
        using var context = NyKontekst(rader);
        var leser = NyLeser(context);

        var poster = await leser
            .ReadNodeAsync(50, new CacheReadOptions { BatchSize = 4, MaxRecords = 10 }, CancellationToken.None)
            .ToListAsync();

        poster.Should().HaveCount(10);
        poster.Select(x => x.CacheRowId).Should().Equal(Enumerable.Range(1, 10));
    }

    [Theory]
    [InlineData(CacheEntityStatus.All, 3)]
    [InlineData(CacheEntityStatus.NotDeleted, 2)]
    [InlineData(CacheEntityStatus.OnlyDeleted, 1)]
    public async Task ReadNodeAsync_filtrerer_paa_slettestatus(CacheEntityStatus status, int forventet)
    {
        using var context = NyKontekst(
            Rad(1, 50),
            Rad(2, 50, isDeleted: true),
            Rad(3, 50));
        var leser = NyLeser(context);

        var poster = await leser
            .ReadNodeAsync(50, new CacheReadOptions { EntityStatus = status }, CancellationToken.None)
            .ToListAsync();

        poster.Should().HaveCount(forventet);
    }

    [Fact]
    public async Task ReadNodeAsync_tar_bare_poster_strengt_nyere_enn_vannmerket()
    {
        using var context = NyKontekst(
            Rad(1, 50, trackDate: Basis),
            Rad(2, 50, trackDate: Basis.AddHours(1)),
            Rad(3, 50, trackDate: Basis.AddHours(2)));
        var leser = NyLeser(context);

        var poster = await leser
            .ReadNodeAsync(50, new CacheReadOptions { ChangedAfter = Basis }, CancellationToken.None)
            .ToListAsync();

        poster.Select(x => x.CacheRowId).Should().Equal([2, 3]);
    }

    /// <summary>
    /// TrackInfoDateImported er ikke unik — hele batcher kan dele tidsstempel. Med
    /// vannmerket alene som sidenøkkel ville poster på batchgrensen blitt lest to
    /// ganger eller hoppet over.
    /// </summary>
    [Fact]
    public async Task ReadNodeAsync_haandterer_mange_poster_med_samme_tidsstempel()
    {
        var felles = Basis.AddHours(5);
        var rader = Enumerable.Range(1, 15).Select(i => Rad(i, 50, trackDate: felles)).ToArray();
        using var context = NyKontekst(rader);
        var leser = NyLeser(context);

        var poster = await leser
            .ReadNodeAsync(
                50,
                new CacheReadOptions { ChangedAfter = Basis, BatchSize = 5 },
                CancellationToken.None)
            .ToListAsync();

        poster.Should().HaveCount(15);
        poster.Select(x => x.CacheRowId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ReadNodeAsync_gir_en_avvist_post_og_fortsetter_naar_blobben_er_odelagt()
    {
        using var context = NyKontekst(
            Rad(1, 50),
            Rad(2, 50, blob: [1, 2, 3, 4]),
            Rad(3, 50, json: "{ikke json"),
            Rad(4, 50));
        var leser = NyLeser(context);

        var poster = await leser.ReadNodeAsync(50, new CacheReadOptions(), CancellationToken.None).ToListAsync();

        poster.Should().HaveCount(4);
        poster.Where(x => x.IsMalformed).Select(x => x.CacheRowId).Should().Equal([2, 3]);
        poster.Where(x => x.IsMalformed).Should().OnlyContain(x => x.Error != null && x.ProxyId != string.Empty);
        poster.Where(x => !x.IsMalformed).Select(x => x.CacheRowId).Should().Equal([1, 4]);
    }

    [Fact]
    public async Task ReadNodeAsync_gir_ingenting_for_en_node_som_ikke_finnes()
    {
        using var context = NyKontekst(Rad(1, 50));
        var leser = NyLeser(context);

        var poster = await leser.ReadNodeAsync(999, new CacheReadOptions(), CancellationToken.None).ToListAsync();

        poster.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNodeStatisticsAsync_teller_og_finner_hoyeste_tidsstempel()
    {
        using var context = NyKontekst(
            Rad(1, 50, trackDate: Basis),
            Rad(2, 50, trackDate: Basis.AddHours(3)),
            Rad(3, 50, isDeleted: true, trackDate: Basis.AddHours(9)),
            Rad(4, 18));

        context.HarvestStates.Add(new CacheHarvestState
        {
            Id = 50,
            Reprocess = false,
            NodeName = "Testnode",
            IsSensitive = true,
            NumberOfRecordsInCache = 3
        });
        context.SaveChanges();

        var leser = NyLeser(context);

        var statistikk = await leser.GetNodeStatisticsAsync(50, CacheEntityStatus.All, CancellationToken.None);

        statistikk.NodeId.Should().Be(50);
        statistikk.RecordCount.Should().Be(3);
        statistikk.MaxTrackInfoDateImported.Should().Be(Basis.AddHours(9));
        statistikk.HasHarvestState.Should().BeTrue();
        statistikk.NodeName.Should().Be("Testnode");
        statistikk.IsSensitive.Should().BeTrue();
        statistikk.NumberOfRecordsInCache.Should().Be(3);
    }

    [Fact]
    public async Task GetNodeStatisticsAsync_taaler_node_uten_harveststate_og_uten_rader()
    {
        using var context = NyKontekst(Rad(1, 50));
        var leser = NyLeser(context);

        var statistikk = await leser.GetNodeStatisticsAsync(999, CacheEntityStatus.All, CancellationToken.None);

        statistikk.RecordCount.Should().Be(0);
        statistikk.MaxTrackInfoDateImported.Should().BeNull();
        statistikk.HasHarvestState.Should().BeFalse();
        statistikk.IsSensitive.Should().BeFalse();
    }

    [Fact]
    public async Task GetNodesAsync_teller_rader_per_node()
    {
        using var context = NyKontekst(Rad(1, 50), Rad(2, 50), Rad(3, 18), Rad(4, 113), Rad(5, 113));
        var leser = NyLeser(context);

        var noder = await leser.GetNodesAsync(CancellationToken.None);

        noder.Should().Equal([(18, 1), (50, 2), (113, 2)]);
    }
}

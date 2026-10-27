using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

public class ImportBatchProcessorTests
{
    /// <summary>Leser som gir postene den har fått, uten database.</summary>
    private sealed class FakeLeser : ICacheRecordReader
    {
        private readonly List<CacheReadItem> _poster;

        public FakeLeser(params CacheReadItem[] poster)
        {
            _poster = [.. poster];
        }

        public CacheReadOptions? BruktOptions { get; private set; }

        public async IAsyncEnumerable<CacheReadItem> ReadNodeAsync(
            int nodeId,
            CacheReadOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            BruktOptions = options;

            foreach (var post in _poster)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return post;
            }

            await Task.CompletedTask;
        }

        public Task<CacheNodeStatistics> GetNodeStatisticsAsync(
            int nodeId, CacheEntityStatus entityStatus, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<(int NodeId, int RecordCount)>> GetNodesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Skriver som husker hva hver runde inneholdt. Lokalitets-Id-ene deles ut
    /// fortløpende, slik en ekte <c>MERGE</c> ville gjort.
    /// </summary>
    private sealed class FakeSkriver : IObservationWriter
    {
        private int _nesteLokalitetsId = 1;

        public List<List<string>> Runder { get; } = [];

        public Task<PublishResult> PublishAsync(
            ImportBatch batch,
            LocationIdCache? locationIds = null,
            CancellationToken cancellationToken = default)
        {
            Runder.Add(batch.Observations.Select(x => x.ProxyId!).ToList());

            var lokaliteter = batch.Locations.Count;

            foreach (var lokalitet in batch.Locations)
            {
                locationIds?.Set(lokalitet.LookupId!, _nesteLokalitetsId++);
            }

            return Task.FromResult(new PublishResult(batch.Count, lokaliteter, batch.Count, TimeSpan.Zero));
        }
    }

    private sealed class FakeUtfall : IImportOutcomeSink
    {
        public List<string> Avviste { get; } = [];

        public List<string> Ødelagte { get; } = [];

        public List<string> Slettede { get; } = [];

        public int Flusher { get; private set; }

        public void RecordRejected(CacheReadItem item, ImportRecordOutcome outcome) => Avviste.Add(item.ProxyId);

        public void RecordMalformed(CacheReadItem item) => Ødelagte.Add(item.ProxyId);

        public void RecordDeleted(CacheReadItem item, string proxyId) => Slettede.Add(proxyId);

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            Flusher++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeFramdrift : IImportProgressSink
    {
        public List<ImportRoundSummary> Runder { get; } = [];

        public Task RoundPublishedAsync(ImportRoundSummary summary, CancellationToken cancellationToken)
        {
            Runder.Add(summary);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Et steg som setter identiteten fra katalognummeret, og avviser postene som er
    /// merket med det.
    /// </summary>
    private sealed class FakeIdentitetssteg : IImportIdentityStep
    {
        public ImportStepType StepType => ImportStepType.Pre;

        public ImportRecordResult ValidateAndImport(
            CacheRecord record,
            ImportedObservation observation,
            ImportContext context)
        {
            observation.ProxyId = record.CatalogNumber;

            observation.Location = new ImportedLocation
            {
                LookupId = record.Locality,
                East = 600000,
                North = 6600000
            };

            return record.Sex == "avvis"
                ? ImportRecordResult.Rejected(RecordValidationCode.Error, RecordNotificationCode.Ok, "nei")
                : ImportRecordResult.Ok();
        }
    }

    private readonly FakeSkriver _skriver = new();
    private readonly FakeUtfall _utfall = new();
    private readonly FakeFramdrift _framdrift = new();

    private static ImportRunSettings Innstillinger => new()
    {
        NodeId = 40,
        ProcessEngineId = 7,
        ProcessedAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
        ImportedAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)
    };

    private static CacheReadItem Post(
        int cacheRowId,
        string katalognummer,
        string lokalitet = "40_600000_6600000_100",
        bool slettet = false,
        bool avvis = false) =>
        CacheReadItem.Ok(cacheRowId, katalognummer, new CacheRecord
        {
            CatalogNumber = katalognummer,
            Locality = lokalitet,
            IsDeleted = slettet,
            Sex = avvis ? "avvis" : null,
            TrackInfoDateImported = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(cacheRowId),
            DateLastModified = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        });

    private ImportBatchProcessor Prosessor(FakeLeser leser, LocationIdCache? buffer = null) =>
        new(
            leser,
            _skriver,
            () => new ImportRecordProcessor([new FakeIdentitetssteg()]),
            NullLogger<ImportBatchProcessor>.Instance,
            _utfall,
            _framdrift,
            buffer);

    [Fact]
    public async Task Tom_node_skriver_ingenting()
    {
        var resultat = await Prosessor(new FakeLeser()).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        resultat.Read.Should().Be(0);
        resultat.Rounds.Should().Be(0);
        _skriver.Runder.Should().BeEmpty();
    }

    [Fact]
    public async Task Skriver_halen_som_en_egen_runde()
    {
        var resultat = await Prosessor(new FakeLeser(Post(1, "a"), Post(2, "b"))).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 250_000 },
            CancellationToken.None);

        resultat.Imported.Should().Be(2);
        resultat.Rounds.Should().Be(1);
        _skriver.Runder.Should().ContainSingle().Which.Should().Equal("a", "b");
    }

    /// <summary>
    /// Bunten skrives når den er full, og halen blir en runde for seg.
    /// </summary>
    [Fact]
    public async Task Skriver_en_runde_per_full_bunt()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"), Post(3, "c"), Post(4, "d"), Post(5, "e"));

        var resultat = await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 2 }, CancellationToken.None);

        resultat.Rounds.Should().Be(3);
        _skriver.Runder.Should().HaveCount(3);
        _skriver.Runder[0].Should().Equal("a", "b");
        _skriver.Runder[1].Should().Equal("c", "d");
        _skriver.Runder[2].Should().Equal("e");
    }

    [Fact]
    public async Task Bunten_tommes_mellom_rundene()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"), Post(3, "c"));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 2 }, CancellationToken.None);

        _skriver.Runder[1].Should().NotContain("a").And.NotContain("b");
    }

    // --- Utfallene ---------------------------------------------------------

    [Fact]
    public async Task Teller_og_rapporterer_avviste_poster()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b", avvis: true));

        var resultat = await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        resultat.Imported.Should().Be(1);
        resultat.Rejected.Should().Be(1);
        _utfall.Avviste.Should().Equal("b");
        _skriver.Runder.Should().ContainSingle().Which.Should().Equal("a");
    }

    [Fact]
    public async Task Teller_og_rapporterer_slettede_poster()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b", slettet: true));

        var resultat = await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        resultat.Deleted.Should().Be(1);
        resultat.Imported.Should().Be(1);
        _utfall.Slettede.Should().Equal("b");
    }

    /// <summary>
    /// En ødelagt blob er én post, ikke en avbrutt node. Det er hele poenget med at
    /// leseren leverer <c>Malformed</c> i stedet for å kaste.
    /// </summary>
    [Fact]
    public async Task En_odelagt_blob_stopper_ikke_noden()
    {
        var leser = new FakeLeser(
            Post(1, "a"),
            CacheReadItem.Malformed(2, "b", "gzip-feil"),
            Post(3, "c"));

        var resultat = await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        resultat.Read.Should().Be(3);
        resultat.Malformed.Should().Be(1);
        resultat.Imported.Should().Be(2);
        _utfall.Ødelagte.Should().Equal("b");
    }

    [Fact]
    public async Task Avviste_poster_skrives_sammen_med_runden_sin()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"), Post(3, "c"), Post(4, "d"));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 2 }, CancellationToken.None);

        _utfall.Flusher.Should().Be(2, "én flush per publisert runde");
    }

    // --- Overstyringene ----------------------------------------------------

    /// <summary>
    /// Overstyringene settes rett etter lesing og før kjeden — identiteten her bygges
    /// av katalognummeret, så en overstyring av det slår gjennom i <c>ProxyId</c>.
    /// </summary>
    [Fact]
    public async Task Setter_feltoverstyringene_for_kjeden()
    {
        var leser = new FakeLeser(Post(1, "a"));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest
            {
                Settings = Innstillinger,
                Overrides = ImportPropertyOverrides.Parse("""{"CatalogNumber":"overstyrt"}""")
            },
            CancellationToken.None);

        _skriver.Runder.Should().ContainSingle().Which.Should().Equal("overstyrt");
    }

    // --- Vannmerket --------------------------------------------------------

    [Fact]
    public async Task Rapporterer_vannmerket_etter_hver_runde()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"), Post(3, "c"), Post(4, "d"));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 2 }, CancellationToken.None);

        _framdrift.Runder.Should().HaveCount(2);
        _framdrift.Runder[0].Round.Should().Be(1);
        _framdrift.Runder[0].LastCacheRowId.Should().Be(2);
        _framdrift.Runder[0].LastCatalogNumber.Should().Be("b");
        _framdrift.Runder[1].LastCacheRowId.Should().Be(4);
    }

    /// <summary>
    /// Vannmerket skal ikke flyttes forbi en post som ble avvist: var avvisningen
    /// forbigående, ville posten aldri blitt forsøkt igjen.
    /// </summary>
    [Fact]
    public async Task Vannmerket_flyttes_ikke_av_en_avvist_post()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b", avvis: true));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        _framdrift.Runder.Should().ContainSingle()
            .Which.LastCacheRowId.Should().Be(1, "post 2 ble avvist");
    }

    [Fact]
    public async Task Rapporterer_trackinfodato_som_vannmerke()
    {
        var leser = new FakeLeser(Post(1, "a"));

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        _framdrift.Runder.Should().ContainSingle()
            .Which.LastTrackInfoDateImported.Should()
            .Be(new DateTime(2026, 10, 1, 0, 1, 0, DateTimeKind.Utc));
    }

    // --- Lokalitetsbufferet ------------------------------------------------

    /// <summary>
    /// Lokaliteten skrives i første runde og slås opp i bufferet i de neste. Det er
    /// forskjellen fra Artskart 2, som ville tatt den med på nytt hver gang.
    /// </summary>
    [Fact]
    public async Task Skriver_en_delt_lokalitet_bare_en_gang()
    {
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"), Post(3, "c"), Post(4, "d"));

        var resultat = await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 2 }, CancellationToken.None);

        resultat.Locations.Should().Be(1, "alle fire postene deler lokalitet");
        _framdrift.Runder[0].Locations.Should().Be(1);
        _framdrift.Runder[1].Locations.Should().Be(0, "lokaliteten var kjent fra forrige runde");
    }

    [Fact]
    public async Task Bruker_bufferet_kalleren_gir()
    {
        var buffer = new LocationIdCache();
        buffer.Set("40_600000_6600000_100", 4711);

        var resultat = await Prosessor(new FakeLeser(Post(1, "a")), buffer).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, CancellationToken.None);

        resultat.Locations.Should().Be(0, "lokaliteten fantes alt");
        buffer.Hits.Should().Be(1);
    }

    // --- Lesingen ----------------------------------------------------------

    [Fact]
    public async Task Sender_lesealternativene_videre()
    {
        var leser = new FakeLeser();
        var vannmerke = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

        await Prosessor(leser).RunAsync(
            new ImportNodeRequest
            {
                Settings = Innstillinger,
                Read = new CacheReadOptions { ChangedAfter = vannmerke, MaxRecords = 10 }
            },
            CancellationToken.None);

        leser.BruktOptions!.ChangedAfter.Should().Be(vannmerke);
        leser.BruktOptions.MaxRecords.Should().Be(10);
    }

    [Fact]
    public async Task Avbrytes_mellom_poster()
    {
        var kilde = new CancellationTokenSource();
        var leser = new FakeLeser(Post(1, "a"), Post(2, "b"));

        await kilde.CancelAsync();

        var handling = async () => await Prosessor(leser).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger }, kilde.Token);

        await handling.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Krever_en_buntstorrelse_over_null()
    {
        var handling = async () => await Prosessor(new FakeLeser()).RunAsync(
            new ImportNodeRequest { Settings = Innstillinger, BatchSize = 0 }, CancellationToken.None);

        await handling.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Kjeden har tilstand som hører til én nodekjøring, så den må bygges per kjøring.
    /// </summary>
    [Fact]
    public async Task Bygger_en_ny_kjede_per_kjoring()
    {
        var antall = 0;

        var prosessor = new ImportBatchProcessor(
            new FakeLeser(Post(1, "a")),
            _skriver,
            () =>
            {
                antall++;

                return new ImportRecordProcessor([new FakeIdentitetssteg()]);
            },
            NullLogger<ImportBatchProcessor>.Instance);

        var forespørsel = new ImportNodeRequest { Settings = Innstillinger };

        await prosessor.RunAsync(forespørsel, CancellationToken.None);
        await prosessor.RunAsync(forespørsel, CancellationToken.None);

        antall.Should().Be(2);
    }
}

using System.Diagnostics;
using Artskart3.Core.Application.Import.Models;
using Microsoft.Extensions.Logging;

namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Kjører én node fra ende til ende: leser cachen, tolker hver post, samler dem opp, og
/// skriver en runde når bunten er full.
///
/// DET ER HER DE TRE DELENE MØTES. <c>CacheRecordReader</c> leverer poster,
/// <see cref="ImportRecordProcessor"/> tolker dem, og <see cref="IObservationWriter"/>
/// skriver dem. Denne klassen eier rekkefølgen og buntingen, og ingenting annet.
///
/// LIGGER I CORE, IKKE I WORKERS — samme begrunnelse som for stegkjeden: alt den
/// trenger er abstraksjoner Core eier, og da kan den enhetstestes med falske lesere og
/// skrivere i stedet for en database. Workers eier jobbregistrering og
/// <c>ImportState</c>.
///
/// STEGKJEDEN BYGGES PER NODE, og det er grunnen til fabrikken i konstruktøren:
/// <c>ImportCheckUniqueIdStep</c> og <c>ImportRecordIdStep</c> har tilstand som hører
/// til én nodekjøring. En gjenbrukt kjede ville avvist poster som dubletter på tvers av
/// noder.
///
/// LOKALITETSBUFFERET DELES PÅ TVERS AV NODER. Nøkkelen bærer nodenummeret for alle
/// noder unntatt 1010, så treffene kommer stort sett innenfor én node — men bufferet
/// koster ingenting å beholde, og for 1010 er gjenbruken reell. Det er ikke trådtrygt,
/// og importkøen har én arbeider, som i Artskart 2.
///
/// FASE 9 ER IKKE MED ENNÅ: <c>ImportState</c>, feilhåndtering og gjenopptakelse hører
/// dit. Krokene står klare — <see cref="IImportOutcomeSink"/> for avviste og slettede
/// poster, <see cref="IImportProgressSink"/> for vannmerket etter hver runde.
/// </summary>
public class ImportBatchProcessor
{
    private readonly ICacheRecordReader _leser;
    private readonly IObservationWriter _skriver;
    private readonly Func<ImportRecordProcessor> _kjedefabrikk;
    private readonly ILogger<ImportBatchProcessor> _logger;
    private readonly IImportOutcomeSink? _utfall;
    private readonly IImportProgressSink? _framdrift;
    private readonly LocationIdCache _lokalitetsbuffer;

    public ImportBatchProcessor(
        ICacheRecordReader leser,
        IObservationWriter skriver,
        Func<ImportRecordProcessor> kjedefabrikk,
        ILogger<ImportBatchProcessor> logger,
        IImportOutcomeSink? utfall = null,
        IImportProgressSink? framdrift = null,
        LocationIdCache? lokalitetsbuffer = null)
    {
        _leser = leser;
        _skriver = skriver;
        _kjedefabrikk = kjedefabrikk;
        _logger = logger;
        _utfall = utfall;
        _framdrift = framdrift;
        _lokalitetsbuffer = lokalitetsbuffer ?? new LocationIdCache();
    }

    public async Task<ImportNodeResult> RunAsync(
        ImportNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BatchSize < 1) throw new ArgumentOutOfRangeException(nameof(request));

        var nodeId = request.Settings.NodeId;
        var klokke = Stopwatch.StartNew();
        var kjede = _kjedefabrikk();
        var bunt = new ImportBatch();
        var telling = new Telling();

        _logger.LogInformation(
            "Starter node {Node}{Inkrementelt}, bunter på {Bunt} poster",
            nodeId,
            request.Read.ChangedAfter is null ? " (full)" : $" (endret etter {request.Read.ChangedAfter:O})",
            request.BatchSize);

        await foreach (var post in _leser.ReadNodeAsync(nodeId, request.Read, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            telling.Lest++;

            if (post.IsMalformed)
            {
                telling.Ødelagte++;
                _utfall?.RecordMalformed(post);

                continue;
            }

            var record = post.Record!;

            // Overstyringene settes her, før kjeden — samme sted som i Artskart 2.
            request.Overrides.ApplyTo(record);

            var utfall = kjede.Process(record, request.Settings);

            switch (utfall.Status)
            {
                case ImportRecordStatus.Imported:
                    telling.Importerte++;
                    bunt.Add(utfall.Observation, _lokalitetsbuffer);
                    telling.Merk(post, record);
                    break;

                case ImportRecordStatus.Rejected:
                    telling.Avviste++;
                    _utfall?.RecordRejected(post, utfall);
                    break;

                case ImportRecordStatus.Deleted:
                    telling.Slettede++;
                    _utfall?.RecordDeleted(post, utfall.Observation.ProxyId!);
                    break;

                case ImportRecordStatus.DeletedWithoutIdentity:
                    telling.SlettedeUtenIdentitet++;
                    break;

                default:
                    throw new InvalidOperationException($"Ukjent utfall {utfall.Status}.");
            }

            if (bunt.Count >= request.BatchSize)
            {
                await Publiser(bunt, telling, cancellationToken);
            }
        }

        // Halen. En node med færre poster enn bunten har bare denne.
        await Publiser(bunt, telling, cancellationToken);

        klokke.Stop();

        var resultat = new ImportNodeResult(
            nodeId,
            telling.Lest,
            telling.Importerte,
            telling.Avviste,
            telling.Slettede,
            telling.SlettedeUtenIdentitet,
            telling.Ødelagte,
            telling.Runder,
            telling.Lokaliteter,
            klokke.Elapsed);

        _logger.LogInformation(
            "Node {Node} ferdig: {Lest} lest, {Importerte} importert, {Avviste} avvist, "
            + "{Slettede} slettet, {Ødelagte} ødelagte, {Runder} runder på {Sekunder} s "
            + "(lokalitetsbuffer: {Treff} treff, {Bom} bom)",
            nodeId,
            telling.Lest,
            telling.Importerte,
            telling.Avviste,
            telling.Slettede,
            telling.Ødelagte,
            telling.Runder,
            Math.Round(klokke.Elapsed.TotalSeconds, 1),
            _lokalitetsbuffer.Hits,
            _lokalitetsbuffer.Misses);

        return resultat;
    }

    /// <summary>
    /// Skriver bunten og tømmer den. En tom bunt gjør ingenting — det er billigere enn
    /// å la kalleren huske å sjekke.
    /// </summary>
    private async Task Publiser(ImportBatch bunt, Telling telling, CancellationToken cancellationToken)
    {
        if (bunt.Count == 0) return;

        var resultat = await _skriver.PublishAsync(bunt, _lokalitetsbuffer, cancellationToken);

        telling.Runder++;
        telling.Lokaliteter += resultat.Locations;

        // Avviste og slettede poster skrives sammen med runden de hører til, slik at en
        // avbrutt kjøring etterlater et sammenhengende bilde.
        if (_utfall is not null) await _utfall.FlushAsync(cancellationToken);

        if (_framdrift is not null)
        {
            await _framdrift.RoundPublishedAsync(
                new ImportRoundSummary(
                    telling.Runder,
                    resultat.Observations,
                    resultat.Locations,
                    resultat.Elapsed,
                    telling.SisteCacheRowId,
                    telling.SisteTrackInfoDateImported,
                    telling.SisteKatalognummer,
                    telling.SisteDateLastModified),
                cancellationToken);
        }

        bunt.Clear();
    }

    /// <summary>
    /// Tellingene og vannmerkene for én nodekjøring.
    /// </summary>
    private sealed class Telling
    {
        public int Lest { get; set; }

        public int Importerte { get; set; }

        public int Avviste { get; set; }

        public int Slettede { get; set; }

        public int SlettedeUtenIdentitet { get; set; }

        public int Ødelagte { get; set; }

        public int Runder { get; set; }

        public int Lokaliteter { get; set; }

        public int SisteCacheRowId { get; private set; }

        public DateTime? SisteTrackInfoDateImported { get; private set; }

        public string? SisteKatalognummer { get; private set; }

        public DateTime? SisteDateLastModified { get; private set; }

        /// <summary>
        /// Flytter vannmerket. BARE IMPORTERTE POSTER TELLER: står vannmerket forbi en
        /// post som ble avvist, og avvisningen skyldtes noe forbigående, ville posten
        /// aldri blitt forsøkt igjen.
        /// </summary>
        public void Merk(CacheReadItem post, CacheRecord record)
        {
            SisteCacheRowId = post.CacheRowId;
            SisteTrackInfoDateImported = record.TrackInfoDateImported;
            SisteKatalognummer = record.CatalogNumber;
            SisteDateLastModified = record.DateLastModified;
        }
    }
}

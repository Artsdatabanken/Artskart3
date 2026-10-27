using System.Diagnostics;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Workers.Import;

/// <summary>
/// Orkestrering: leser cache → prosesserer → skriver til index.
///
/// ÉN NODE OM GANGEN. <see cref="ImportNodeAsync"/> kjører én node fra ende til ende,
/// og <see cref="ImportActiveNodesAsync"/> går gjennom dem etter tur. Importkøen har
/// én arbeider, som i Artskart 2, så ingenting her er trådtrygt og ingenting skal
/// kjøre parallelt.
///
/// LESESTIEN FRA FASE 1 LIGGER FORTSATT HER. <see cref="ReadNodeAsync"/> leser og
/// teller uten å skrive, og den er fremdeles den billigste måten å se om cachen svarer
/// som forventet.
///
/// TILSTANDEN KOMMER I FASE 9. Vannmerket leses ikke fra <c>ImportState</c> ennå —
/// kalleren oppgir det — og avviste og slettede poster telles, men lagres ikke. Begge
/// krokene står klare i <see cref="ImportBatchProcessor"/>.
/// </summary>
public class ImportService
{
    private readonly ICacheRecordReader _cacheRecordReader;
    private readonly IDataSourceRegistry _dataSources;
    private readonly ImportStepChainFactory _kjedefabrikk;
    private readonly IObservationWriter _skriver;
    private readonly IOrganizationCreator _organisasjonsoppretter;
    private readonly IReferenceVariantWriter _variantskriver;
    private readonly IArtsKartDbContext _context;
    private readonly ImportOptions _innstillinger;
    private readonly TimeProvider _klokke;
    private readonly ILogger<ImportService> _logger;
    private readonly ILogger<ImportBatchProcessor> _prosessorlogger;

    /// <summary>
    /// Deles på tvers av nodene i én kjøring. Nøkkelen bærer nodenummeret for alle
    /// noder unntatt 1010, så treffene kommer stort sett innenfor én node — men
    /// bufferet koster ingenting å beholde.
    /// </summary>
    private readonly LocationIdCache _lokalitetsbuffer = new();

    public ImportService(
        ICacheRecordReader cacheRecordReader,
        IDataSourceRegistry dataSources,
        ImportStepChainFactory kjedefabrikk,
        IObservationWriter skriver,
        IOrganizationCreator organisasjonsoppretter,
        IReferenceVariantWriter variantskriver,
        IArtsKartDbContext context,
        ImportOptions innstillinger,
        TimeProvider klokke,
        ILogger<ImportService> logger,
        ILogger<ImportBatchProcessor> prosessorlogger)
    {
        _cacheRecordReader = cacheRecordReader;
        _dataSources = dataSources;
        _kjedefabrikk = kjedefabrikk;
        _skriver = skriver;
        _organisasjonsoppretter = organisasjonsoppretter;
        _variantskriver = variantskriver;
        _context = context;
        _innstillinger = innstillinger;
        _klokke = klokke;
        _logger = logger;
        _prosessorlogger = prosessorlogger;
    }

    /// <summary>
    /// Kjører nodene i <see cref="ImportOptions.Nodes"/>, eller alle som ikke er merket
    /// slettet i <c>DataSource</c>.
    ///
    /// EN NODE SOM FEILER STOPPER IKKE DE ANDRE. Feilhåndteringen per node — og
    /// <c>FailCount</c>, som deaktiverer en kilde etter hver femte feil — hører til
    /// fase 9; her logges feilen og neste node tas.
    /// </summary>
    public async Task<IReadOnlyList<ImportNodeResult>> ImportActiveNodesAsync(
        CancellationToken cancellationToken)
    {
        var noder = _innstillinger.Nodes.Length > 0
            ? _innstillinger.Nodes
            : (await _dataSources.ActiveNodesAsync(cancellationToken)).Select(x => x.NodeId).ToArray();

        if (noder.Length == 0)
        {
            _logger.LogWarning(
                "Ingen noder å importere. DataSource-tabellen er tom, og den fylles av speilingen "
                + "fra Artskart 2 (plan 4.9.2) — ikke av importen.");

            return [];
        }

        _logger.LogInformation("Importerer {Antall} noder: {Noder}", noder.Length, string.Join(", ", noder));

        var resultater = new List<ImportNodeResult>();

        foreach (var nodeId in noder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                resultater.Add(await ImportNodeAsync(nodeId, changedAfter: null, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception feil)
            {
                _logger.LogError(feil, "Node {Node} feilet. Fortsetter med neste.", nodeId);
            }
        }

        _logger.LogInformation(
            "Kjøringen er ferdig: {Noder} av {Totalt} noder gikk gjennom, {Importerte} poster importert, "
            + "{Avviste} avvist",
            resultater.Count,
            noder.Length,
            resultater.Sum(x => x.Imported),
            resultater.Sum(x => x.Rejected));

        return resultater;
    }

    /// <summary>
    /// Kjører én node fra cache til index.
    /// </summary>
    /// <param name="nodeId">Noden.</param>
    /// <param name="changedAfter">
    /// Vannmerke for inkrementell lesing: bare poster endret etter dette tas med.
    /// <c>null</c> leser hele noden. Fra fase 9 kommer verdien fra
    /// <c>ImportState.TrackDateOfLastRecordProcessed</c>.
    /// </param>
    /// <param name="cancellationToken">Avbrytes mellom poster, ikke midt i en runde.</param>
    public async Task<ImportNodeResult> ImportNodeAsync(
        int nodeId,
        DateTime? changedAfter,
        CancellationToken cancellationToken)
    {
        var konfig = await _dataSources.ByNodeIdAsync(nodeId, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"Node {nodeId} finnes ikke i DataSource. Registeret fylles av speilingen "
                         + "fra Artskart 2 (plan 4.9.2).");

        KrevAtMaskeringenErForsvarlig(konfig);

        var motorId = await HentProsessmotorId(cancellationToken);
        var nå = _klokke.GetUtcNow().UtcDateTime;

        _logger.LogInformation(
            "Node {Node} ({Navn}): motor {Motor}, {Modus}, {Overstyringer} feltoverstyringer, "
            + "oppretter organisasjoner: {Oppretter}",
            nodeId,
            konfig.Name ?? "uten navn",
            motorId,
            changedAfter is null ? "full" : $"endret etter {changedAfter:O}",
            konfig.PropertyOverrides.Count,
            _innstillinger.CreateMissingOrganizations);

        var prosessor = new ImportBatchProcessor(
            _cacheRecordReader,
            _skriver,
            Byggkjede,
            _prosessorlogger,
            utfall: null,
            framdrift: null,
            _lokalitetsbuffer);

        var forespørsel = new ImportNodeRequest
        {
            Settings = new ImportRunSettings
            {
                NodeId = nodeId,
                ProcessEngineId = motorId,
                ProcessedAt = nå,
                ImportedAt = nå,
                IsDatasetSensitive = konfig.IsSensitive
            },
            Read = new CacheReadOptions { ChangedAfter = changedAfter },
            Overrides = konfig.PropertyOverrides,
            BatchSize = _innstillinger.BatchSize
        };

        return await prosessor.RunAsync(forespørsel, cancellationToken);
    }

    /// <summary>
    /// Stegkjeden for én nodekjøring. Oppretteren og variantskriveren utelates når
    /// konfigurasjonen sier det — se <see cref="ImportOptions"/>.
    /// </summary>
    private ImportRecordProcessor Byggkjede() =>
        _kjedefabrikk.Create(
            _innstillinger.CreateMissingOrganizations ? _organisasjonsoppretter : null,
            _innstillinger.RecordUnrecognisedVariants ? _variantskriver : null);

    /// <summary>
    /// SPERREN FRA PLANEN. Maskeringen av sensitive arter er fase 8 og finnes ikke
    /// ennå, så en kjøring mot ekte data ville skrevet eksakte koordinater for arter
    /// som skal maskeres. Den må slås av med vilje, og aldri for en node som er merket
    /// sensitiv i sin helhet.
    /// </summary>
    private void KrevAtMaskeringenErForsvarlig(DataSourceConfig konfig)
    {
        if (konfig.IsSensitive)
        {
            throw new InvalidOperationException(
                $"Node {konfig.NodeId} er merket sensitiv, og maskeringen (fase 8) er ikke implementert. "
                + "Importen ville skrevet eksakte koordinater for hele datasettet.");
        }

        if (_innstillinger.AllowImportWithoutMasking) return;

        throw new InvalidOperationException(
            "Maskeringen av sensitive arter (fase 8) er ikke implementert, så importen skriver "
            + "eksakte koordinater også for sensitive arter. Sett Import:AllowImportWithoutMasking "
            + "for å kjøre mot testdata.");
    }

    /// <summary>
    /// <c>CurrentProcessEngineId</c> er <c>MAX(Id)</c> fra <c>ProsessEngineHistory</c>,
    /// og den avgjør om en eksisterende post regnes som behandlet av denne versjonen
    /// (plan 4.7.3). Bumpes den, blir neste kjøring i praksis en full reimport — så
    /// importen leser den, og rører den aldri.
    /// </summary>
    private async Task<int> HentProsessmotorId(CancellationToken cancellationToken)
    {
        var id = await _context.Set<ProsessEngineHistory>()
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (id == 0)
        {
            throw new InvalidOperationException(
                "ProsessEngineHistory er tom. Uten en prosessmotor-Id kan ingen post merkes med "
                + "hvilken versjon av tolkningen som skrev den (plan 4.7.3).");
        }

        return id;
    }

    /// <summary>
    /// Leser gjennom én node og logger hva cachen inneholder. Skriver ingenting.
    /// </summary>
    public async Task<CacheReadSummary> ReadNodeAsync(
        int nodeId,
        int? maxRecords,
        CancellationToken cancellationToken)
    {
        var statistikk = await _cacheRecordReader.GetNodeStatisticsAsync(
            nodeId, CacheEntityStatus.All, cancellationToken);

        _logger.LogInformation(
            "Node {NodeId} ({NodeName}): {RecordCount} rader i cachen, siste harvest-tidsstempel {MaxTrackDate}. " +
            "HarvestState funnet: {HasHarvestState}, sensitiv kilde: {IsSensitive}, harvests eget tall: {NumberOfRecordsInCache}",
            statistikk.NodeId,
            statistikk.NodeName ?? "ukjent navn",
            statistikk.RecordCount,
            statistikk.MaxTrackInfoDateImported,
            statistikk.HasHarvestState,
            statistikk.IsSensitive,
            statistikk.NumberOfRecordsInCache);

        var options = new CacheReadOptions
        {
            EntityStatus = CacheEntityStatus.All,
            MaxRecords = maxRecords
        };

        var klokke = Stopwatch.StartNew();
        var rader = 0;
        var poster = 0;
        var ødelagte = 0;
        var slettede = 0;
        DateTime? minTrackDate = null;
        DateTime? maxTrackDate = null;
        var harLoggetFørstePost = false;

        await foreach (var item in _cacheRecordReader.ReadNodeAsync(nodeId, options, cancellationToken))
        {
            rader++;

            if (item.Record is null)
            {
                ødelagte++;

                // Hver ødelagte rad logges. Er de mange, er det ikke dataene som er
                // gale — det er lesestien, og da er antallet det første sporet.
                _logger.LogError(
                    "Node {NodeId}: kunne ikke lese cache-rad {CacheRowId} ({ProxyId}): {Error}",
                    nodeId, item.CacheRowId, item.ProxyId, item.Error);
                continue;
            }

            poster++;
            var record = item.Record;

            if (record.IsDeleted) slettede++;

            if (minTrackDate is null || record.TrackInfoDateImported < minTrackDate)
                minTrackDate = record.TrackInfoDateImported;
            if (maxTrackDate is null || record.TrackInfoDateImported > maxTrackDate)
                maxTrackDate = record.TrackInfoDateImported;

            if (!harLoggetFørstePost)
            {
                harLoggetFørstePost = true;

                // Artsnavnet er med fordi det viser at blobben faktisk ble pakket ut
                // riktig, æøå inkludert. Koordinater og lokalitet logges ikke — de kan
                // tilhøre en sensitiv art, og en loggfil er ingen maskering.
                _logger.LogInformation(
                    "Node {NodeId}: første post {ProxyId} | art: {ScientificName} | institusjon: {InstitutionCode} | " +
                    "har koordinat: {HarKoordinat} | har geometri: {HarGeometri} | ukjente felt: {UkjenteFelt}",
                    nodeId,
                    item.ProxyId,
                    record.ScientificName ?? "(tom)",
                    record.InstitutionCode ?? "(tom)",
                    record.Latitude != 0 || record.Longitude != 0,
                    !string.IsNullOrWhiteSpace(record.FootprintWkt),
                    record.UnknownFields?.Count ?? 0);
            }
        }

        klokke.Stop();

        var oppsummering = new CacheReadSummary
        {
            NodeId = nodeId,
            RowsRead = rader,
            RecordsRead = poster,
            MalformedCount = ødelagte,
            DeletedCount = slettede,
            MinTrackInfoDateImported = minTrackDate,
            MaxTrackInfoDateImported = maxTrackDate,
            Elapsed = klokke.Elapsed
        };

        _logger.LogInformation(
            "Node {NodeId} lest: {RowsRead} rader, {RecordsRead} poster, {DeletedCount} slettet, " +
            "{MalformedCount} ulesbare, tidsrom {MinTrackDate} til {MaxTrackDate}, {ElapsedMs} ms. Ingenting er skrevet.",
            oppsummering.NodeId,
            oppsummering.RowsRead,
            oppsummering.RecordsRead,
            oppsummering.DeletedCount,
            oppsummering.MalformedCount,
            oppsummering.MinTrackInfoDateImported,
            oppsummering.MaxTrackInfoDateImported,
            (long)oppsummering.Elapsed.TotalMilliseconds);

        return oppsummering;
    }
}

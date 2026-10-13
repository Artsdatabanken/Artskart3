using System.Diagnostics;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Workers.Import;

/// <summary>
/// Orkestrering: leser cache → prosesserer → skriver til index.
///
/// FORELØPIG BARE LESESTIEN (fase 1). <see cref="ReadNodeAsync"/> leser én node og
/// teller, uten å skrive noe som helst. Steg-kjeden, lokaliteter og skriving til
/// index kommer i fase 4–6.
/// </summary>
public class ImportService
{
    private readonly ICacheRecordReader _cacheRecordReader;
    private readonly ILogger<ImportService> _logger;

    public ImportService(ICacheRecordReader cacheRecordReader, ILogger<ImportService> logger)
    {
        _cacheRecordReader = cacheRecordReader;
        _logger = logger;
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

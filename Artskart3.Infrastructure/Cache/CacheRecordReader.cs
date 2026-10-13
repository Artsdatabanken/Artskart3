using System.Runtime.CompilerServices;
using System.Text.Json;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Infrastructure.Cache.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Cache;

/// <summary>
/// Leser poster for én node fra cachen: batchvis spørring, dekomprimering og
/// deserialisering.
///
/// INGEN SENSITIVITETSSPERRE HER. Artskart 2 har en gate i <c>SqlCacheDatabaseService</c>
/// som nekter å levere poster fra en node med <c>HarvestStates.IsSensitive = 1</c> —
/// men importen kaller den med <c>includeSensitiveDataset: true</c> og ser derfor alle
/// noder. Gaten er til for de åpne API-ene. Porteres den hit, forsvinner hele
/// sensitive datakilder stille ut av importen. Maskeringen er fase 8s ansvar, ikke
/// lesestiens.
///
/// SIDING PÅ NØKKEL, IKKE OFFSET. Artskart 2 bruker <c>Skip(pageIndex * pageSize)</c>,
/// som får SQL Server til å lese og forkaste alt foran hver side: for node 1010 med
/// 37 millioner poster er den siste siden 37 millioner forkastede rader. Resultatsettet
/// her er det samme — samme filter, samme rekkefølge — men hver batch starter der
/// forrige sluttet.
/// </summary>
public class CacheRecordReader : ICacheRecordReader
{
    private readonly ArtskartCacheDbContext _context;
    private readonly ILogger<CacheRecordReader> _logger;

    public CacheRecordReader(ArtskartCacheDbContext context, ILogger<CacheRecordReader> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async IAsyncEnumerable<CacheReadItem> ReadNodeAsync(
        int nodeId,
        CacheReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.BatchSize, 1);

        var inkrementelt = options.ChangedAfter.HasValue;
        var lest = 0;
        var sisteId = 0;
        DateTime? sisteTrackDate = null;

        while (options.MaxRecords is null || lest < options.MaxRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var antallIBatch = options.MaxRecords is int maks
                ? Math.Min(options.BatchSize, maks - lest)
                : options.BatchSize;

            var batch = await NesteBatchAsync(
                nodeId, options, inkrementelt, sisteId, sisteTrackDate, antallIBatch, cancellationToken);

            if (batch.Count == 0) yield break;

            foreach (var rad in batch)
            {
                yield return LesRad(rad);
            }

            lest += batch.Count;
            sisteId = batch[^1].Id;
            sisteTrackDate = batch[^1].TrackInfoDateImported;

            _logger.LogDebug(
                "Node {NodeId}: lest {Lest} poster fra cache (siste rad-id {SisteId})",
                nodeId, lest, sisteId);

            // En kortere batch enn bedt om betyr at kilden er tom. Uten denne
            // sjekken koster hver node én tom spørring ekstra.
            if (batch.Count < antallIBatch) yield break;
        }
    }

    public async Task<CacheNodeStatistics> GetNodeStatisticsAsync(
        int nodeId,
        CacheEntityStatus entityStatus,
        CancellationToken cancellationToken)
    {
        var spørring = Filtrer(_context.DarwinCoreProxies.AsNoTracking(), nodeId, entityStatus, changedAfter: null);

        var antall = await spørring.CountAsync(cancellationToken);
        var maksTrackDate = await spørring
            .MaxAsync(x => (DateTime?)x.TrackInfoDateImported, cancellationToken);

        // Reprocess-raden er harvests midlertidige kopi. Den ordinære er den som
        // beskriver noden.
        var harvestState = await _context.HarvestStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId && !x.Reprocess, cancellationToken);

        return new CacheNodeStatistics
        {
            NodeId = nodeId,
            RecordCount = antall,
            MaxTrackInfoDateImported = maksTrackDate,
            HasHarvestState = harvestState is not null,
            NodeName = harvestState?.NodeName,
            IsSensitive = harvestState?.IsSensitive ?? false,
            NumberOfRecordsInCache = harvestState?.NumberOfRecordsInCache ?? 0
        };
    }

    /// <summary>
    /// Full aggregering over hele tabellen. På en produksjonscache er det titalls
    /// millioner rader og minutter med kjøretid — den er til for å måle dekningen i en
    /// nedlastet kopi, ikke for å kalles underveis i en import.
    /// </summary>
    public async Task<IReadOnlyList<(int NodeId, int RecordCount)>> GetNodesAsync(
        CancellationToken cancellationToken)
    {
        var rader = await _context.DarwinCoreProxies
            .AsNoTracking()
            .GroupBy(x => x.NodeId)
            .Select(g => new { NodeId = g.Key, RecordCount = g.Count() })
            .OrderBy(x => x.NodeId)
            .ToListAsync(cancellationToken);

        return rader.Select(x => (x.NodeId, x.RecordCount)).ToList();
    }

    private Task<List<CacheDarwinCoreProxy>> NesteBatchAsync(
        int nodeId,
        CacheReadOptions options,
        bool inkrementelt,
        int sisteId,
        DateTime? sisteTrackDate,
        int antall,
        CancellationToken cancellationToken)
    {
        var spørring = Filtrer(
            _context.DarwinCoreProxies.AsNoTracking(), nodeId, options.EntityStatus, options.ChangedAfter);

        if (inkrementelt)
        {
            // Vannmerket er ikke unikt — mange poster deler samme
            // TrackInfoDateImported — så nøkkelen må være (TrackInfoDateImported, Id).
            // Med bare tidsstempelet ville poster på batchgrensen enten blitt lest to
            // ganger eller hoppet over.
            if (sisteTrackDate is DateTime sist)
            {
                spørring = spørring.Where(x =>
                    x.TrackInfoDateImported > sist ||
                    (x.TrackInfoDateImported == sist && x.Id > sisteId));
            }

            return spørring
                .OrderBy(x => x.TrackInfoDateImported)
                .ThenBy(x => x.Id)
                .Take(antall)
                .ToListAsync(cancellationToken);
        }

        return spørring
            .Where(x => x.Id > sisteId)
            .OrderBy(x => x.Id)
            .Take(antall)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<CacheDarwinCoreProxy> Filtrer(
        IQueryable<CacheDarwinCoreProxy> spørring,
        int nodeId,
        CacheEntityStatus entityStatus,
        DateTime? changedAfter)
    {
        spørring = spørring.Where(x => x.NodeId == nodeId);

        spørring = entityStatus switch
        {
            CacheEntityStatus.NotDeleted => spørring.Where(x => !x.IsDeleted),
            CacheEntityStatus.OnlyDeleted => spørring.Where(x => x.IsDeleted),
            _ => spørring
        };

        if (changedAfter is DateTime etter)
        {
            spørring = spørring.Where(x => x.TrackInfoDateImported > etter);
        }

        return spørring;
    }

    private static CacheReadItem LesRad(CacheDarwinCoreProxy rad)
    {
        try
        {
            var json = CacheBlobCompression.Decompress(rad.SerializedObject);
            var record = CacheRecordSerializer.Deserialize(json);

            // Begge hentes fra kolonnene, ikke fra blobben — slik
            // SqlDarwinCoreProxy.ToDomainObject gjør det for Hash.
            record.HashCode = rad.Hash;
            record.CacheRowId = rad.Id;

            return CacheReadItem.Ok(rad.Id, rad.ProxyId, record);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            return CacheReadItem.Malformed(rad.Id, rad.ProxyId, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

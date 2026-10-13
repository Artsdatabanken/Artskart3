using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Lesestien fra cache-databasen. Implementasjonen eier EF og dekomprimeringen;
/// importen ser bare poster.
/// </summary>
public interface ICacheRecordReader
{
    /// <summary>
    /// Leser én node som en strøm. Poster kommer i stigende rekkefølge etter
    /// cache-radens <c>Id</c>, eller etter <c>TrackInfoDateImported</c> når
    /// <see cref="CacheReadOptions.ChangedAfter"/> er satt.
    /// </summary>
    IAsyncEnumerable<CacheReadItem> ReadNodeAsync(
        int nodeId,
        CacheReadOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Teller noden uten å pakke ut en eneste blob.
    /// </summary>
    Task<CacheNodeStatistics> GetNodeStatisticsAsync(
        int nodeId,
        CacheEntityStatus entityStatus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Nodene som faktisk finnes i cache-kopien, med antall rader per node. Grunnlaget
    /// for dekningsmålingen mot produksjon i paritetsriggen.
    /// </summary>
    Task<IReadOnlyList<(int NodeId, int RecordCount)>> GetNodesAsync(
        CancellationToken cancellationToken);
}

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Hva som skal leses fra én cache-node.
/// </summary>
public class CacheReadOptions
{
    /// <summary>Hvilke poster som skal med. Standard er alle, som i normal import.</summary>
    public CacheEntityStatus EntityStatus { get; set; } = CacheEntityStatus.All;

    /// <summary>
    /// Vannmerke for inkrementell lesing: bare poster med
    /// <c>TrackInfoDateImported</c> strengt større enn denne tas med. <c>null</c>
    /// leser hele noden.
    ///
    /// Satt vannmerke endrer også rekkefølgen — se <c>CacheRecordReader</c>.
    /// </summary>
    public DateTime? ChangedAfter { get; set; }

    /// <summary>
    /// Øvre grense for antall poster. Brukes av gullsettene i paritetsriggen, der
    /// node 1010 er begrenset til 200 000 poster. <c>null</c> betyr ingen grense.
    /// </summary>
    public int? MaxRecords { get; set; }

    /// <summary>
    /// Antall rader per spørring mot cachen. Hver rad bærer en komprimert blob, så
    /// dette styrer minnebruken under lesing mer enn noe annet.
    /// </summary>
    public int BatchSize { get; set; } = 5000;
}

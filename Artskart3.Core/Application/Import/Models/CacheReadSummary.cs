namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Oppsummering av én gjennomlesing av en node. Ingen poster skrives noe sted — dette
/// er tallene som viser at lesestien virker mot en ekte cache.
/// </summary>
public class CacheReadSummary
{
    public int NodeId { get; init; }

    /// <summary>Rader lest fra cachen, inkludert de som ikke kunne pakkes ut.</summary>
    public int RowsRead { get; init; }

    /// <summary>Poster som ble pakket ut og deserialisert.</summary>
    public int RecordsRead { get; init; }

    /// <summary>Rader der blobben ikke kunne leses. Skal være 0.</summary>
    public int MalformedCount { get; init; }

    /// <summary>Poster merket slettet i cachen.</summary>
    public int DeletedCount { get; init; }

    public DateTime? MinTrackInfoDateImported { get; init; }

    public DateTime? MaxTrackInfoDateImported { get; init; }

    public TimeSpan Elapsed { get; init; }
}

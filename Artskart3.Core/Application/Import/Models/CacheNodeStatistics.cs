namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Hva cachen inneholder for én node. Port av Artskart 2s
/// <c>StatisticsForCacheNode</c>, utvidet med radene fra <c>HarvestStates</c>.
///
/// Tallene trengs to steder: for å måle dekningen i den lokale cache-kopien mot
/// produksjon (paritetsriggen, plan 4.8.3), og for å se om et vannmerke har noe
/// arbeid bak seg før en kjøring settes i gang.
/// </summary>
public class CacheNodeStatistics
{
    public int NodeId { get; init; }

    /// <summary>Antall rader i cachen for noden, etter det valgte slettefilteret.</summary>
    public int RecordCount { get; init; }

    /// <summary>
    /// Høyeste <c>TrackInfoDateImported</c> for noden. Dette er taket en inkrementell
    /// kjøring kan nå. <c>null</c> når noden ikke har rader.
    /// </summary>
    public DateTime? MaxTrackInfoDateImported { get; init; }

    /// <summary>
    /// Raden i <c>HarvestStates</c> finnes ikke for alle noder i en nedlastet
    /// cache-kopi. Er den <c>null</c>, er feltene under uten verdi.
    /// </summary>
    public bool HasHarvestState { get; init; }

    public string? NodeName { get; init; }

    /// <summary>
    /// Sensitiv datakilde. Importen leser slike noder som alle andre — flagget gjelder
    /// de åpne API-ene i Artskart 2, ikke importen. Se <c>CacheRecordReader</c>.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>Harvests eget tall. Kan avvike fra <see cref="RecordCount"/>.</summary>
    public int NumberOfRecordsInCache { get; init; }
}

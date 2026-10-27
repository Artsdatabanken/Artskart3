using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Alt én nodekjøring trenger å vite.
/// </summary>
public class ImportNodeRequest
{
    /// <summary>Det som er likt for alle poster i kjøringen.</summary>
    public required ImportRunSettings Settings { get; init; }

    /// <summary>
    /// Hva som skal leses: hele noden, eller bare det som har endret seg siden
    /// vannmerket.
    /// </summary>
    public CacheReadOptions Read { get; init; } = new();

    /// <summary>
    /// Feltoverstyringene fra datakilderegisteret. Settes på hver post rett etter
    /// lesing, før stegkjeden — samme sted som i Artskart 2.
    /// </summary>
    public ImportPropertyOverrides Overrides { get; init; } = ImportPropertyOverrides.None;

    /// <summary>
    /// Hvor mange poster som samles opp før en runde skrives. Se
    /// <c>ObservationWriterOptions.BatchSize</c> for hvorfor den er så stor.
    /// </summary>
    public int BatchSize { get; init; } = 250_000;
}

/// <summary>
/// Hva en nodekjøring endte med. Tallene er grunnlaget for <c>ImportState</c> og for
/// sammenligningen mot Artskart 2 i paritetsriggen.
/// </summary>
/// <param name="NodeId">Noden som ble kjørt.</param>
/// <param name="Read">Antall cache-rader som ble lest.</param>
/// <param name="Imported">Antall poster som gikk gjennom hele kjeden.</param>
/// <param name="Rejected">Antall poster et steg avviste.</param>
/// <param name="Deleted">Antall slettede poster med identitet, altså noe å fjerne.</param>
/// <param name="DeletedWithoutIdentity">
/// Antall slettede poster uten identitet. Det finnes ingenting å fjerne, og Artskart 2
/// hopper stille over dem.
/// </param>
/// <param name="Malformed">Antall cache-rader som ikke kunne pakkes ut.</param>
/// <param name="Rounds">Antall staging-runder som ble publisert.</param>
/// <param name="Locations">Antall lokaliteter som ble skrevet, summert over rundene.</param>
/// <param name="Elapsed">Tiden hele noden tok.</param>
public sealed record ImportNodeResult(
    int NodeId,
    int Read,
    int Imported,
    int Rejected,
    int Deleted,
    int DeletedWithoutIdentity,
    int Malformed,
    int Rounds,
    int Locations,
    TimeSpan Elapsed);

/// <summary>
/// Hva én publisert runde endte med, og hvor langt kjøringen er kommet.
///
/// VANNMERKENE ER POENGET. Planen (4.7.4) vil ha en markør for siste publiserte runde,
/// slik at en avbrutt import ikke må begynne på noden på nytt. Feltene her er de samme
/// Artskart 2 bruker til det: <c>TrackInfoDateImported</c> for den inkrementelle
/// grensen, og katalognummer pluss endringsdato for å finne igjen stedet i en node.
/// </summary>
/// <param name="Round">Rundens nummer i denne nodekjøringen, fra 1.</param>
/// <param name="Observations">Antall observasjoner i runden.</param>
/// <param name="Locations">Antall lokaliteter runden skrev.</param>
/// <param name="Elapsed">Tiden runden tok.</param>
/// <param name="LastCacheRowId">Cache-radens Id for den siste posten i runden.</param>
/// <param name="LastTrackInfoDateImported">
/// <c>TrackInfoDateImported</c> for den siste posten. Blir
/// <c>ImportState.TrackDateOfLastRecordProcessed</c>.
/// </param>
/// <param name="LastCatalogNumber">Blir <c>ImportState.LastRecordImportedCatalogNumber</c>.</param>
/// <param name="LastDateLastModified">Blir <c>ImportState.LastRecordDateLastModified</c>.</param>
public sealed record ImportRoundSummary(
    int Round,
    int Observations,
    int Locations,
    TimeSpan Elapsed,
    int LastCacheRowId,
    DateTime? LastTrackInfoDateImported,
    string? LastCatalogNumber,
    DateTime? LastDateLastModified);

/// <summary>
/// Tar imot postene som ikke ble importert.
///
/// DE SKRIVES IKKE HER. Artskart 2 lagrer dem i <c>RejectedRecord</c> og
/// <c>DeletedRecords</c>, og det hører til fase 9 sammen med <c>ImportState</c>.
/// Grensesnittet finnes nå fordi tellingene alene ikke er nok: en avvist post som
/// ingen kan slå opp er stille datatap, og det er hele grunnen til at tabellen finnes.
///
/// METODENE ER SYNKRONE MED VILJE. De kalles per post — opptil 1,4 millioner ganger i
/// en full kjøring, målt på Artskart 2s eksisterende avvisninger — så de skal samle opp
/// i minnet og skrive i <see cref="FlushAsync"/>, som kalles én gang per runde.
/// </summary>
public interface IImportOutcomeSink
{
    /// <summary>Et steg avviste posten. Observasjonen er halvbygd, slik Artskart 2 lagrer den.</summary>
    void RecordRejected(CacheReadItem item, ImportRecordOutcome outcome);

    /// <summary>Cache-raden kunne ikke pakkes ut. Posten er aldri tolket.</summary>
    void RecordMalformed(CacheReadItem item);

    /// <summary>Posten er slettet i cachen og skal fjernes fra index.</summary>
    void RecordDeleted(CacheReadItem item, string proxyId);

    /// <summary>Skriver det som er samlet opp. Kalles én gang per publisert runde.</summary>
    Task FlushAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Får beskjed når en runde er publisert, og er stedet gjenopptakelsen hektes på.
/// </summary>
public interface IImportProgressSink
{
    Task RoundPublishedAsync(ImportRoundSummary summary, CancellationToken cancellationToken);
}

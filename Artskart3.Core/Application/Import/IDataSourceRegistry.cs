namespace Artskart3.Core.Application.Import;

/// <summary>
/// Datakilderegisteret, lest fra <c>DataSource</c>. Svarer på hvilke noder importen
/// skal kjøre og hvordan den enkelte noden skal behandles.
///
/// IKKE BUFRET. Registeret redigeres utenfor importen — i Artskart 2s admin-flate
/// gjennom overgangen, og av harvest ved hver kjøring. En node som er satt sensitiv
/// mellom to kjøringer skal være sensitiv ved neste kjøring, ikke ved neste omstart.
/// Tabellen er 39 rader.
/// </summary>
public interface IDataSourceRegistry
{
    /// <summary>
    /// Konfigurasjonen for en node, eller <c>null</c> om noden ikke finnes i
    /// registeret.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nodens <c>ImportPropertyOverrides</c> er ugyldig. Da skal noden ikke kjøre.
    /// </exception>
    Task<DataSourceConfig?> ByNodeIdAsync(int nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nodene importen skal kjøre, altså alle som ikke er merket slettet.
    /// Overstyringene leses ikke her — en ugyldig konfigurasjon på én node skal ikke
    /// gjøre det umulig å liste de andre.
    /// </summary>
    Task<IReadOnlyList<DataSourceSummary>> ActiveNodesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Det importen trenger å vite om en node.
/// </summary>
/// <param name="NodeId">Nodenummeret. Blir <c>Observation.NodeId</c>.</param>
/// <param name="Name">Nodens navn, til logg og rapportering.</param>
/// <param name="IsSensitive">
/// Hele datasettet er sensitivt. Da maskeres alle poster fra noden, uavhengig av art.
/// </param>
/// <param name="GbifDataSetId">
/// Nøkkelen annotasjoner hentes på. Vedlikeholdes av harvest, så den kan endre seg
/// mellom kjøringer.
/// </param>
/// <param name="GbifDoi">Datasettets DOI, vises i kildehenvisningen.</param>
/// <param name="PropertyOverrides">
/// Feltoverstyringene, ferdig lest og validert. Settes på hver post rett etter lesing
/// fra cache, før annotasjoner og før stegkjeden.
/// </param>
/// <param name="NonValidOccurrenceIds">
/// Kilden har <c>occurrenceID</c>-er som ikke er til å stole på. Harvest tømmer feltet,
/// så identiteten utledes av katalognummeret.
/// </param>
public sealed record DataSourceConfig(
    int NodeId,
    string? Name,
    bool IsSensitive,
    Guid GbifDataSetId,
    string? GbifDoi,
    ImportPropertyOverrides PropertyOverrides,
    bool NonValidOccurrenceIds);

/// <summary>
/// En node slik den ser ut i en liste over hva som kan kjøres.
/// </summary>
public sealed record DataSourceSummary(int NodeId, string? Name, bool IsSensitive, int UpdateFrequencyInDays);

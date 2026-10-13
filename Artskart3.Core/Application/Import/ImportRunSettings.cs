namespace Artskart3.Core.Application.Import;

/// <summary>
/// Det som er likt for alle poster i én kjøring. Tilsvarer de feltene Artskart 2s
/// <c>ProcessContext</c> bidrar med når en ny observasjon opprettes.
/// </summary>
public class ImportRunSettings
{
    /// <summary>Datakilden postene kommer fra.</summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// <c>ProsessEngineHistory.Id</c> for den kjørende importmotoren. Endres den, ser
    /// importen alle poster som behandlet av en annen versjon og gjør dem på nytt
    /// (plan 4.7.3).
    /// </summary>
    public required int ProcessEngineId { get; init; }

    /// <summary>
    /// Tidspunktet kjøringen regnes som utført på. Artskart 2 setter det én gang per
    /// kjøring.
    /// </summary>
    public required DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Tidspunktet postene regnes som importert på.
    ///
    /// Artskart 2 setter <c>DateTime.Now</c> per post. Her er det én verdi for hele
    /// kjøringen: ingenting leser millisekundene, og en fast verdi gjør utdataene
    /// deterministiske — som er verdt noe når to kjøringer skal sammenlignes rad for
    /// rad i paritetsriggen.
    /// </summary>
    public required DateTime ImportedAt { get; init; }

    /// <summary>
    /// Om datakilden er merket sensitiv. Gjelder uavhengig av art, og avgjør maskering
    /// i fase 8.
    /// </summary>
    public bool IsDatasetSensitive { get; init; }
}

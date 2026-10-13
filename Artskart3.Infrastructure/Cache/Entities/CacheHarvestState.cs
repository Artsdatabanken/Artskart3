namespace Artskart3.Infrastructure.Cache.Entities;

/// <summary>
/// Raden i <c>Artskart2Cache.HarvestStates</c> — harvests tilstand per node.
///
/// SAMMENSATT NØKKEL (<see cref="Id"/>, <see cref="Reprocess"/>). Hver node kan ha to
/// rader: den ordinære og en midlertidig for reprosessering, slik at reprosessering
/// kan gå samtidig med vanlig harvest.
///
/// VI SKRIVER IKKE HIT. Importen i Artskart 2 oppdaterer vannmerkene her; Artskart 3
/// leser cachen read-only og fører sin egen tilstand i <c>ImportState</c> i
/// indeksbasen. Bare feltene vi faktisk leser er mappet.
/// </summary>
public class CacheHarvestState
{
    public int Id { get; set; }

    public bool Reprocess { get; set; }

    public string? NodeName { get; set; }

    /// <summary>
    /// Mappet som int med vilje. Enumen (<c>Normal</c>, <c>Disabled</c>,
    /// <c>Deleted</c>, <c>ErrorDisabled</c> …) hører til datakilderegisteret, som
    /// bygges i fase 2 — ikke til lesestien.
    /// </summary>
    public int UpdateMode { get; set; }

    public int NumberOfRecordsInCache { get; set; }

    /// <summary>
    /// Sensitiv datakilde. Gjelder de åpne API-ene i Artskart 2, ikke importen — den
    /// leser slike noder som alle andre. Se <c>CacheRecordReader</c>.
    /// </summary>
    public bool IsSensitive { get; set; }
}

namespace Artskart3.Core.Application.Import.Models;

/// <summary>Hva kjeden endte med for én post.</summary>
public enum ImportRecordStatus
{
    /// <summary>Alle steg godtok posten. <see cref="ImportRecordOutcome.Observation"/> er klar til skriving.</summary>
    Imported,

    /// <summary>
    /// Et steg avviste posten. Observasjonen er med likevel, halvbygd — Artskart 2
    /// bruker den til å fylle <c>RejectedRecord</c>, som altså lagrer det importen
    /// rakk å tolke.
    /// </summary>
    Rejected,

    /// <summary>
    /// Posten er slettet i cachen. Bare identitetssteget har kjørt, og
    /// <c>ProxyId</c> er det eneste som er satt — nok til å fjerne raden fra index.
    /// </summary>
    Deleted,

    /// <summary>
    /// Posten er slettet i cachen, men identiteten kunne ikke bestemmes. Da finnes det
    /// ingenting å slette, og Artskart 2 hopper stille over den.
    /// </summary>
    DeletedWithoutIdentity
}

/// <summary>
/// Resultatet av å kjøre kjeden over én post. Ingenting er skrevet; dette er det
/// fase 6 tar imot.
/// </summary>
public class ImportRecordOutcome
{
    public required ImportRecordStatus Status { get; init; }

    public required ImportedObservation Observation { get; init; }

    /// <summary>
    /// Avvisningene, i den rekkefølgen stegene ga dem. Tom når posten ble importert.
    /// Merk at kjeden stopper ved første avvisning i en fase, så lista har sjelden mer
    /// enn ett element — unntaket er den parallelle fasen, der alle steg får kjøre
    /// ferdig før posten avvises.
    /// </summary>
    public List<ImportRecordResult> Errors { get; } = [];

    /// <summary>
    /// Advarsler. De stopper ingenting, og i Artskart 2 havner de i et eget
    /// <c>WarningRecord</c>-spor, ikke på observasjonen.
    /// </summary>
    public List<ImportRecordResult> Warnings { get; } = [];

    public bool IsImported => Status == ImportRecordStatus.Imported;
}

using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public class SavedFilter : BaseEntity
{
    /// <summary>
    /// Id-en som brukes utad (API, lenker). Int-nøkkelen eksponeres ikke, så
    /// lenker til filtre kan ikke gjettes.
    /// </summary>
    public Guid PublicId { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Serialisert ObservationSearchFilterDto (JSON). Lagrede filtre leses tilbake
    /// lenge etter at de ble skrevet, så DTO-en kan bare få nye nullable felt —
    /// aldri omdøpte eller fjernede.
    /// </summary>
    public string FilterJson { get; set; } = null!;

    // Kartutsnitt i EPSG:25833. Null hvis kartet ikke var tegnet da filteret ble lagret.
    public double? MinX { get; set; }
    public double? MinY { get; set; }
    public double? MaxX { get; set; }
    public double? MaxY { get; set; }

    public bool IsDefault { get; set; }
}

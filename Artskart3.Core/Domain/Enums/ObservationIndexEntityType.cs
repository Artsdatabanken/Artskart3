namespace Artskart3.Core.Domain.Enums;

/// <summary>
/// Entitetstypene i ObservationEntityIndex. Speiler AreaType.
/// </summary>
public enum ObservationIndexEntityType
{
    Municipality = 1,
    County = 2,
    RestrictedArea = 3,
    OceanArea = 4,

    /// <summary>
    /// Ingen kode bruker verdien, men radene finnes — 3 644 617 av dem. Typen har
    /// ingen filterdimensjon, så de kan ikke velges, bare telles med.
    /// </summary>
    NorwaysMarineAreas = 5,

    SvalbardBjørnøyaAndJanMayen = 6,
}

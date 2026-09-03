namespace Artskart3.Core.Domain.Enums;

/// <summary>
/// Entitetstyper brukt i ObservationEntityIndex.
/// 1-99: Områdetyper (speiler AreaType-enumen direkte).
/// 100+: Organisasjonstyper (100 + OrganizationType-verdi).
/// Denne separasjonen gjør at nye områdetyper og organisasjonstyper kan legges til uten kollisjonsrisiko.
/// </summary>
public enum ObservationIndexEntityType
{
    // Områdetyper (speiler AreaType-enumen)
    Municipality = 1,
    County = 2,
    RestrictedArea = 3,
    OceanArea = 4,
    SvalbardBjørnøyaAndJanMayen = 6,

    // Organisasjonstyper (100 + OrganizationType-verdi)
    Institution = 101
}

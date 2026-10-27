namespace Artskart3.Core.Application.Import;

/// <summary>
/// Utfallet av å validere én post i ett steg. Port av Artskart 2s
/// <c>RecordValidationCode</c>.
///
/// TALLVERDIENE ER LAGRET. Enumen persisteres som int i
/// <c>ProcessRecordResult.ValidationCode</c> og <c>ObservationError.ValidationCode</c>,
/// og forvaltningsflaten filtrerer på den. Rekkefølgen er derfor Artskart 2s, og nye
/// koder skal legges til nederst — aldri midt i.
///
/// <see cref="Ok"/>, <see cref="Warn"/> og <see cref="SensitiveSpecies"/> er de tre
/// kodene som lar posten fortsette gjennom kjeden. Alt annet avviser den.
/// </summary>
public enum RecordValidationCode
{
    Ok = 0,
    Warn = 1,
    Error = 2,
    InstitutionCodeMissing = 3,
    InstitutionCodeNotKnown = 4,
    CollectionCodeMissing = 5,
    CatalogNumberIsMissing = 6,
    DuplicateId = 7,
    CoordinateOutOfBounds = 8,
    CoordinateMissing = 9,
    BasisOfRecordMissing = 10,
    BasisOfRecordValueUnknown = 11,
    NoTaxonomicMatch = 12,
    BehaviorValueUnknown = 13,
    DatetimeOutOfRange = 14,
    DatasetNameMissing = 15,

    /// <summary>
    /// Posten gjelder en sensitiv art. Ikke en avvisning — maskeringen er resultatet.
    /// </summary>
    SensitiveSpecies = 16,

    NoDatetime = 17,
    Annotation = 18,

    /// <summary>
    /// Organisasjonen posten viser til finnes ikke, og opprettelse er slått av.
    ///
    /// NY KODE I ARTSKART 3. Artskart 2 oppretter alltid en ukjent samling eller et
    /// ukjent datasett og har derfor ingen tilsvarende kode. Denne brukes bare i
    /// opprett-ikke-modus under parallellverifiseringen (plan 4.8), der et avvik her er
    /// et hull i speilingen av registeret.
    /// </summary>
    OrganizationNotCreated = 19
}

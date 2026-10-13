namespace Artskart3.Core.Application.Import;

/// <summary>
/// Hvorfor en validering endte som den gjorde — koden som gjør to avvisninger med
/// samme <see cref="RecordValidationCode"/> til å skille lag. Port av Artskart 2s
/// <c>RecordNotificationCode</c>.
///
/// TALLVERDIENE ER LAGRET, på samme måte som for valideringskodene: de ligger i
/// <c>ProcessRecordResult.NotificationCode</c>, som i tillegg har en indeks på
/// kolonnen. Rekkefølgen er Artskart 2s.
/// </summary>
public enum RecordNotificationCode
{
    Ok = 0,
    CollectionCodeAdded = 1,
    MissingLongitudeLatitude = 2,
    InvalidMgrsNotation = 3,
    NoHitOnRequiredAreadatasets = 4,
    NoHitOnSimpleNorwayBoundary = 5,
    DateTimeCollectedError = 6,
    NoTaxonGroupHit = 7,
    MissingScientificName = 8,
    SqlErrorOnGettingAreas = 9,
    DateTimeIdentifiedError = 10,
    InvalidLongitudeLatitude = 11,
    DatasetAdded = 12,
    DateLastModifiedError = 13,
    NotAbleToMatchTaxonomy = 14,
    CountryNameNotInNorway = 15,
    CountOfDatasetIdsAndNamesDiffers = 16,
    StateProvinceIsJanMayenButCoordinateElsewhere = 17,
    RecordShouldBeHidden = 18,
    RecordShouldBeMasked = 19,
    DatasetShouldBeMasked = 20
}

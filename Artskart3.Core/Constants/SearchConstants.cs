namespace Artskart3.Core.Constants;

public static class SearchConstants
{
    // Taxon search constants
    public const int DefaultMaxTaxonCount = 20;
    public const int MaxTaxonCount = 1000;
    public const int MinTaxonResults = 1;
    public const int DefaultMaxOrganizationCount = 10;
    public const int MaxOrganizationCount = 50;

    // Location search constants
    public const int DefaultMaxLocations = 100000;
    public const int MaxLocationResults = 100000;
    public const int MinLocationResults = 1;

    // Polygon search constants (geometry is expensive to transfer and render)
    public const int DefaultMaxPolygons = 2000;
    public const int MaxPolygonResults = 5000;

    // Observation search constants
    public const int DefaultMaxObservations = 20;
    public const int MaxObservationResults = 10000;
    public const int MinObservationResults = 1;
    // Norge har ~356 kommuner og ~2800 verneområder — 500 dekker alle realistiske filtre med margin,
    // og begrenser størrelsen på SQL IN-klausuler for å hindre misbruk mot dette anonyme endepunktet.
    public const int MaxFilterArraySize = 500;

    // ObservationIds har sin egen, høyere grense. Verdiene kommer fra
    // katalognummer-oppslaget, ikke fra en avkrysningsliste: ett katalognummer kan
    // peke på opptil 675 observasjoner (målt), og maxCount i oppslaget er 50. Med
    // 500 ville et helt legitimt valg blitt avvist. Grensen finnes fortsatt for å
    // begrense IN-klausulen på dette anonyme endepunktet.
    public const int MaxObservationIdFilterSize = 5000;

    // Minstelengde for katalognummer-oppslaget, håndhevet på serveren.
    // 1 tegn ville krevd DISTINCT over millioner av indeksrader for hvert
    // tastetrykk; 2 er nok til at prefikssøket blir et smalt range seek.
    public const int MinCatalogNumberSearchLength = 2;

    // Coordinate constants
    public const int DefaultEpsgCode = 25833;

    // Error messages
    public const string CoordinatePrecisionInvalidMessage = "CoordinatePrecisionFrom must be less than or equal to CoordinatePrecisionTo.";
    public const string ServiceUnavailableMessage = "An error occurred while processing your request. Please try again later.";
    public const string UnexpectedErrorMessage = "An unexpected error occurred while processing your request. Please try again later.";
}

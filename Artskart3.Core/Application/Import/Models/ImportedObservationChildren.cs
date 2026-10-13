using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// En feilrad på observasjonen (<c>ObservationError</c>). Observasjonen importeres
/// likevel — raden forteller hva som var galt med den.
/// </summary>
public class ImportedObservationError
{
    public RecordValidationCode ValidationCode { get; set; }

    public string? ErrorContext { get; set; }

    /// <summary>Satt når feilen kom fra en annotasjon (fase 10).</summary>
    public int? AnnotationId { get; set; }
}

/// <summary>
/// En mediefil knyttet til observasjonen (<c>MediaFile</c>). Importen laster ikke ned
/// noe; den registrerer opphavet og lar nedlastingskøen ta resten.
/// </summary>
public class ImportedMediaFile
{
    /// <summary>URL-en til filen. <c>Origin</c> i tabellen, og <c>NOT NULL</c>.</summary>
    public string Origin { get; set; } = string.Empty;

    public int MediaFileTypeId { get; set; }

    public string? License { get; set; }

    public string? RightsHolder { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// Kobling til en annen observasjon (<c>ObservationLink</c>), fra
/// <c>associatedOccurrences</c>. Den andre observasjonen finnes ikke nødvendigvis
/// ennå, så koblingen bærer referansen som tekst til den kan løses opp.
/// </summary>
public class ImportedObservationLink
{
    /// <summary>Referansen slik kilden oppgav den.</summary>
    public string? LinkedProxyId { get; set; }

    public string? Remarks { get; set; }
}

/// <summary>
/// De umaskerte verdiene for en sensitiv observasjon
/// (<c>SensitiveObservationData</c>). Observasjonen selv får den maskerte posisjonen;
/// denne raden er den eneste veien tilbake til den eksakte.
/// </summary>
public class ImportedSensitiveData
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public int? CoordinatePrecisionInMeters { get; set; }

    public string? Locality { get; set; }

    public string? Notes { get; set; }

    public DateTime? DateTimeCollected { get; set; }

    public Geometry? Geometry { get; set; }
}

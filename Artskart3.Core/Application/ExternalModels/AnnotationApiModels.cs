namespace Artskart3.Core.Application.ExternalModels;

/// <summary>
/// Forespørselen til /api/public-annotations/forObservation. Tjenesten avviser null i
/// tekstfeltene, men godtar tomme strenger.
/// </summary>
public class AnnotationObservationProperties
{
    public string OccurrenceId { get; set; } = "";
    public Guid GbifDataSetId { get; set; }
    public string ScientificName { get; set; } = "";
    public DateTime ObservationCollected { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string StateProvince { get; set; } = "";
    public bool IsMarine { get; set; }
    public bool IsTerrestrial { get; set; }
    public bool IsPreservedSpecimen { get; set; }
}

/// <summary>Bare feltene vi viser eller filtrerer på.</summary>
public class PublicAnnotation
{
    public int Id { get; set; }
    public string? Comment { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? DateCreated { get; set; }
    public List<PublicAnnotationField>? Fields { get; set; }
    public PublicAnnotationActions? Actions { get; set; }

    /// <summary>Tom når annoteringen gjelder etter regler (art, datasett, område) og ikke enkeltfunn.</summary>
    public List<PublicAnnotationObservation>? Observations { get; set; }
}

public class PublicAnnotationField
{
    public string Name { get; set; } = "";
    public string? OriginalValue { get; set; }
    public string? NewValue { get; set; }
}

public class PublicAnnotationActions
{
    public bool AcceptChanges { get; set; }
}

public class PublicAnnotationObservation
{
    public string? ObservationProxyId { get; set; }
}

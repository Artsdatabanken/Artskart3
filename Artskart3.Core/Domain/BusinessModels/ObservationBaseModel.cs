
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;
public abstract class ObservationBaseModel
{
    public int Id { get; set; }
    public string? ProxyId { get; set; }
}

public class ObservationWithNodeModel : ObservationBaseModel
{
    public int NodeId { get; set; }
}

public class ObservationModel : ObservationBaseModel
{
    public int SourceDataId { get; set; }
    public int? InstitutionId { get; set; }
    public int? CollectionId { get; set; }
    public ICollection<int> TaxonIds { get; set; } = new List<int>();
    public ICollection<int> DatasetIds { get; set; } = new List<int>();
    public int? TaxonGroupId { get; set; }
    public BasisOfRecord BasisOfRecord { get; set; }
    public Category Category { get; set; }
    public ICollection<string> Behaviors { get; set; } = new List<string>();
    public ICollection<string> Tags { get; set; } = new List<string>();
    
    // Temporal information
    public DateTime LastModified { get; set; }
    public DateTime? Collected { get; set; }
    public DateTime? Identified { get; set; }
    
    // Identification and collection information
    public string? Uid { get; set; }
    public string? CatalogNumber { get; set; }
    public string? IdentifiedBy { get; set; }
    public string? IdentificationQualifier { get; set; }
    public string? Collector { get; set; }

    // Specimen information
    public string? Habitat { get; set; }
    public string? Sex { get; set; }
    public string? IndividualCount { get; set; }
    public string? CollectingMethod { get; set; }

    // Record metadata
    public string? RecordNumber { get; set; }
    public string? FieldNumber { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? GeoreferenceRemarks { get; set; }
    public string? Preparations { get; set; }
    public string? OtherCatalogNumbers { get; set; }

    // Related resources
    public string? RelatedResourceId { get; set; }
    public string? RelationshipOfResource { get; set; }
    public string? TypeStatus { get; set; }
    public string? EventTime { get; set; }

    // Elevation information
    public int? MaximumElevationInMeters { get; set; }
    public int? MinimumElevationInMeters { get; set; }
    public string? VerbatimDepth { get; set; }

    // Occurrence and processing information
    public string? OccurrenceId { get; set; }
    public int HashCode { get; set; }
    public int ProcessEngineId { get; set; }

    // Status flags
    public bool HasErrors { get; set; }
    public bool HasAnnotations { get; set; }

    // Additional properties
    public string? DynamicProperties { get; set; }
    public string[]? AssociatedReferences { get; set; }

    // Display and classification
    public string? CategoryUrl { get; set; }
    public string? Locality { get; set; }
    public string? MatchedScientificName { get; set; }
    public string? MatchedScientificNameAuthor { get; set; }

    // Links and quality
    public ICollection<ObservationLink> LinkedObservations { get; set; } = new List<ObservationLink>();
    public ObservationQuality? ObservationQuality { get; set; }
    public ICollection<int> MediaFileIds { get; set; } = new List<int>();
    
    public ObservationModel()
    {
    }
}

public class ObservationWithLocationModel : ObservationModel
{
    public int LocationId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int NodeId { get; set; }
    public string? DoiId { get; set; }
    public Guid GBIFDataSetId { get; set; }
}

public class ObservationLink
{
    public bool IsSource { get; set; }
    public string? OccurrenceId { get; set; }
}

public class ObservationQuality
{
    public int Score { get; set; }
    public string? Status { get; set; }
    public ICollection<string> Issues { get; set; } = new List<string>();
}

public enum BasisOfRecord
{
    Unknown = 0,
    HumanObservation = 1,
    PreservedSpecimen = 2,
    FossilSpecimen = 3,
    LivingSpecimen = 4,
    MaterialSample = 5,
    MachineObservation = 6
}
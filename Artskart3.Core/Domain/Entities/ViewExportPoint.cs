using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ViewExportPoint : BaseEntity
{
    public string? ProxyId { get; set; }

    public DateTime? DateTimeCollected { get; set; }

    public int NodeId { get; set; }

    public string? InstitutionId { get; set; }

    public string? InstitutionCode { get; set; }

    public string? CollectionCode { get; set; }

    public string? CatalogNumber { get; set; }

    public DateTime? DatetimeIdentified { get; set; }

    public int? CoordinatePrecisionInMeters { get; set; }

    public string? OccurenceId { get; set; }

    public string? DatasetName { get; set; }

    public string? DatasetId { get; set; }

    public string? Collector { get; set; }

    public string? IndividualCount { get; set; }

    public string? Notes { get; set; }

    public string? IdentifiedBy { get; set; }

    public string? Locality { get; set; }

    public string? Habitat { get; set; }

    public string? Sex { get; set; }

    public string? DateTimeCollectedStr { get; set; }

    public string AssociatedReferences { get; set; } = null!;

    public string? CollectingMethod { get; set; }

    public string? RecordNumber { get; set; }

    public string? FieldNumber { get; set; }

    public string? MeasurementMethod { get; set; }

    public string? GeoreferenceRemarks { get; set; }

    public string? Preparations { get; set; }

    public string? OtherCatalogNumbers { get; set; }

    public string? RelatedResourceId { get; set; }

    public string? RelationshipOfResource { get; set; }

    public string? TypeStatus { get; set; }

    public string? EventTime { get; set; }

    public int? MaximumElevationInMeters { get; set; }

    public int? MinimumElevationInMeters { get; set; }

    public string? VerbatimDepth { get; set; }

    public string? DynamicProperties { get; set; }

    public string Kategori { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string TaxonGroupName { get; set; } = null!;

    public string ScientificName { get; set; } = null!;

    public string? ScientificNameAuthorship { get; set; }

    public string Rank { get; set; } = null!;

    public string? LookupId { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? CoordinatePrecision { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public string? LocalityName { get; set; }

    public DateTime TimeStamp { get; set; }

    public string? Polularname { get; set; }

    public int Unspontaneus { get; set; }

    public int UnsureIdentification { get; set; }

    public int Validated { get; set; }

    public int HasImage { get; set; }

    public int Absent { get; set; }

    public int NotRecovered { get; set; }

    public int Moving { get; set; }

    public int Feeding { get; set; }

    public int Dead { get; set; }

    public int Stationary { get; set; }

    public int Reproductive { get; set; }

    public int Possiblereproductive { get; set; }
}

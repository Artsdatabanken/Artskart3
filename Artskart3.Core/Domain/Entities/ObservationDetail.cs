using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ObservationDetail : BaseEntity
{
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

    public virtual Observation IdNavigation { get; set; } = null!;
}

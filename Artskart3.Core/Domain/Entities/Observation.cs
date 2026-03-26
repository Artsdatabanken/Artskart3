using System;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.Entities;

public partial class Observation
{
    public int Id { get; set; }

    public string? ProxyId { get; set; }

    public DateTime DateLastModified { get; set; }

    public DateTime? DateTimeCollected { get; set; }

    public DateTime DateTimeRecordImported { get; set; }

    public DateTime DateTimeRecordProsessed { get; set; }

    public int NodeId { get; set; }

    public string? InstitutionId { get; set; }

    public string? InstitutionCode { get; set; }

    public string? CollectionCode { get; set; }

    public string? CatalogNumber { get; set; }

    public int BasisOfRecordId { get; set; }

    public DateTime? DatetimeIdentified { get; set; }

    public int TaxonId { get; set; }

    public int MatchedScientificNameId { get; set; }

    public int TaxonGroupId { get; set; }

    public int? CategoryId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int? CoordinatePrecisionInMeters { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public int? LocationId { get; set; }

    public string? OccurenceId { get; set; }

    public int HashCode { get; set; }

    public int ProcessEngineId { get; set; }

    public int? YearCollected { get; set; }

    public int? MonthCollected { get; set; }

    public bool HasErrors { get; set; }

    public bool HasAnnotations { get; set; }

    public int? ObservationQualityTypeId { get; set; }

    public virtual BasisOfRecord BasisOfRecord { get; set; } = null!;

    public virtual Category? Category { get; set; }

    public virtual Location? Location { get; set; }

    public virtual TaxonName MatchedScientificName { get; set; } = null!;

    public virtual ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public virtual ObservationDetail? ObservationDetail { get; set; }

    public virtual ICollection<ObservationError> ObservationErrors { get; set; } = new List<ObservationError>();

    public virtual ICollection<ObservationLink> ObservationLinkLinkedObservations { get; set; } = new List<ObservationLink>();

    public virtual ICollection<ObservationLink> ObservationLinkObservations { get; set; } = new List<ObservationLink>();

    public virtual ObservationQualityType? ObservationQualityType { get; set; }

    public virtual ICollection<OrganizationRelation> OrganizationRelations { get; set; } = new List<OrganizationRelation>();

    public virtual SensitiveObservationDatum? SensitiveObservationDatum { get; set; }

    public virtual Taxon Taxon { get; set; } = null!;

    public virtual ICollection<Behavior> Behaviors { get; set; } = new List<Behavior>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

    public virtual ICollection<Taxon> Taxons { get; set; } = new List<Taxon>();
}

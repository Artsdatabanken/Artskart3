using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Taxon : BaseEntity
{
    public DateTime DateTimeUpdated { get; set; }

    public int TaxonRankId { get; set; }

    public int TaxonId { get; set; }

    public int ParentTaxonId { get; set; }

    public int ValidScientificNameId { get; set; }

    public string? ValidScientificName { get; set; }

    public string? ValidScientificNameAuthorship { get; set; }

    public string? PrefferedPopularname { get; set; }

    public int TaxonGroupId { get; set; }

    public bool ExistsInCountry { get; set; }

    public string ScientificNameIdHiarchy { get; set; } = null!;

    public string TaxonIdHiarchy { get; set; } = null!;

    public int? ObservationCount { get; set; }

    public int? CumulativeObservationCount { get; set; }

    public int? WeeklyObservationCount { get; set; }

    public int? WeeklyCumulativeObservationCount { get; set; }

    public int? DailyObservationCount { get; set; }

    public int? DailyCumulativeObservationCount { get; set; }

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();

    public virtual TaxonGroup TaxonGroup { get; set; } = null!;

    public virtual ICollection<TaxonName> TaxonNames { get; set; } = new List<TaxonName>();

    public virtual ICollection<TaxonPopularName> TaxonPopularNames { get; set; } = new List<TaxonPopularName>();

    public virtual ICollection<TaxonProperty> TaxonProperties { get; set; } = new List<TaxonProperty>();

    public virtual TaxonRank TaxonRank { get; set; } = null!;

    public virtual ICollection<Observation> ObservationsNavigation { get; set; } = new List<Observation>();
}

using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class TaxonName : BaseEntity
{
    public bool Accepted { get; set; }

    public string ScientificName { get; set; } = null!;

    public string? ScientificNameAuthorship { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public int? TaxonId { get; set; }

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();

    public virtual Taxon? Taxon { get; set; }
}

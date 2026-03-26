using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class TaxonProperty : BaseEntity
{
    public string TagGroup { get; set; } = null!;

    public string Prefix { get; set; } = null!;

    public string Context { get; set; } = null!;

    public string Tag { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? ScientificName { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public int? TaxonId { get; set; }

    public virtual Taxon? Taxon { get; set; }
}

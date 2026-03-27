using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class TaxonGroup : BaseEntity
{
    public string Name { get; set; } = null!;

    public int? ObservationCount { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<Taxon> Taxons { get; set; } = new List<Taxon>();
}

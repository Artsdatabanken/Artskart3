using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class TaxonPopularName : BaseEntity
{
    public int SourceId { get; set; }

    public string Language { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool Preffered { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public int? TaxonId { get; set; }

    public virtual Taxon? Taxon { get; set; }
}

using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class BasisOfRecord : BaseEntity
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Variants { get; set; } = null!;

    public int? ObservationCount { get; set; }

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();
}

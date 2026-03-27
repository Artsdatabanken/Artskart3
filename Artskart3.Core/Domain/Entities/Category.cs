using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Category : BaseEntity
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int CategoryTypeId { get; set; }

    public int? ObservationCount { get; set; }

    public virtual CategoryType CategoryType { get; set; } = null!;

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();
}

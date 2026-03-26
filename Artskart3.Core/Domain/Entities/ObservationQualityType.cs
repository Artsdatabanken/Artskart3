using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ObservationQualityType : BaseEntity
{
    public string Value { get; set; } = null!;

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();
}

using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ObservationLink : BaseEntity
{
    public int ObservationId { get; set; }

    public int? LinkedObservationId { get; set; }

    public string? Remarks { get; set; }

    public virtual Observation? LinkedObservation { get; set; }

    public virtual Observation Observation { get; set; } = null!;
}

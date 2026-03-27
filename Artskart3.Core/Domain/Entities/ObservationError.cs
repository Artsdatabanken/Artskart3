using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ObservationError: BaseEntity
{    public int ObservationId { get; set; }

    public int ValidationCode { get; set; }

    public string? ErrorContext { get; set; }

    public DateTime DateTimeModified { get; set; }

    public int? AnnotationId { get; set; }

    public virtual Observation Observation { get; set; } = null!;
}

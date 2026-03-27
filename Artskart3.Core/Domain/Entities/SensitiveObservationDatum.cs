using System;
using System.Collections.Generic;

namespace Artskart3.Core.Domain.Entities;

public partial class SensitiveObservationDatum
{
    public int ObservationId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int? CoordinatePrecisionInMeters { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public string? Locality { get; set; }

    public string? Notes { get; set; }

    public DateTime? DateTimeCollected { get; set; }

    public virtual Observation Observation { get; set; } = null!;
}

using Artskart3.Core.Domain.Entities.Base;
using NetTopologySuite.Geometries;

namespace Artskart3.Core.Domain.Entities;

public partial class Location : BaseEntity
{
    public string? LookupId { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? CoordinatePrecision { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public string? Locality { get; set; }

    public DateTime TimeStamp { get; set; }

    public int NodeId { get; set; }

    public string? LocationId { get; set; }

    public Geometry? Geometry { get; set; }

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();

    public virtual ICollection<LocationArea> LocationAreas { get; set; } = new List<LocationArea>();

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}

public class LocationArea
{
    public int LocationId { get; set; }
    public int AreaId { get; set; }

    public virtual Location Location { get; set; } = null!;
    public virtual Area Area { get; set; } = null!;
}

using Artskart3.Core.Domain.Enums;
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

    /// <summary>
    /// Geometritypen, beregnet og persistert i databasen fra Geometry.
    ///
    /// Databasen eier verdien — kolonnen er PERSISTED og kan ikke settes fra
    /// koden. Se LocationGeometryType for hvorfor den finnes.
    /// </summary>
    public LocationGeometryType GeometryTypeId { get; private set; }

    public virtual ICollection<Observation> Observations { get; set; } = new List<Observation>();

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}

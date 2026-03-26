using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Area : BaseEntity
{

    public string DocumentId { get; set; } = null!;

    public string Fid { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int AreaTypeId { get; set; }

    public string ParentFid { get; set; } = null!;

    public DateTime SyncDateTime { get; set; }

    public int? ObservationCount { get; set; }

    public string Bbox { get; set; } = null!;

    public string GmBbox { get; set; } = null!;

    public DateTime TimeStamp { get; set; }

    public bool IsCurrent { get; set; }

    public virtual AreaType AreaType { get; set; } = null!;

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}

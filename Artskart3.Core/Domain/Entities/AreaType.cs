using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class AreaType : BaseEntity
{

    public string Name { get; set; } = null!;

    public bool IsRequired { get; set; }

    public string CategoryName { get; set; } = null!;

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
}

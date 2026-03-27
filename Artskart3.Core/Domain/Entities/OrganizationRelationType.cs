using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class OrganizationRelationType : BaseEntity
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<OrganizationRelation> OrganizationRelations { get; set; } = new List<OrganizationRelation>();
}

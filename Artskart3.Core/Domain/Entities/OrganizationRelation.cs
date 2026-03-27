using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class OrganizationRelation : BaseEntity
{
    public int ObservationId { get; set; }

    public int OrganizationId { get; set; }

    public int RelationTypeId { get; set; }

    public virtual Observation Observation { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;

    public virtual OrganizationRelationType RelationType { get; set; } = null!;
}

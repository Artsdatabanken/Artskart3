using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Organization : BaseEntity
{
    public int? ParentId { get; set; }

    public string? ExternalId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int OrganizationTypeId { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }

    public int? ObservationCount { get; set; }

    public virtual ICollection<Organization> InverseParent { get; set; } = new List<Organization>();

    public virtual ICollection<OrganizationRelation> OrganizationRelations { get; set; } = new List<OrganizationRelation>();

    public virtual OrganizationType OrganizationType { get; set; } = null!;

    public virtual Organization? Parent { get; set; }
}

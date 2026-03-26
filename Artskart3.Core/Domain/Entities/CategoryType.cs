using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class CategoryType : BaseEntity
{
    public string Name { get; set; } = null!;

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}

using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class Filter : BaseEntity
{
    public string Name { get; set; } = null!;

    public string SerializedFilter { get; set; } = null!;
}

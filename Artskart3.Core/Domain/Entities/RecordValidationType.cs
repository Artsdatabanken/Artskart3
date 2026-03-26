using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class RecordValidationType : BaseEntity
{
    public string Value { get; set; } = null!;

    public bool Deleted { get; set; }
}

using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class TaxonomyState : BaseEntity
{
    public DateTime LastEventProcessedTimeStamp { get; set; }
}

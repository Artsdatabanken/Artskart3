using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class DeletedItem : BaseEntity
{
    public string RecordId { get; set; } = null!;

    public DateTime TimeStamp { get; set; }

    public int ReplicationId { get; set; }
}

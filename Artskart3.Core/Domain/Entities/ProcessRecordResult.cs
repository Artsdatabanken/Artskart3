using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ProcessRecordResult : BaseEntity
{
    public string ErrorContext { get; set; } = null!;

    public int NotificationCode { get; set; }

    public int ValidationCode { get; set; }

    public int? RejectedRecordId { get; set; }

    public virtual RejectedRecord? RejectedRecord { get; set; }
}

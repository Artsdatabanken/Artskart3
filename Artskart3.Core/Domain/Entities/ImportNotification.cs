using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ImportNotification : BaseEntity
{
    public DateTime Timestamp { get; set; }

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;

    public int Severity { get; set; }

    public string SeverityDescription { get; set; } = null!;
}

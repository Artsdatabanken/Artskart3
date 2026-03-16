using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ExportStatus : BaseEntity
{
    public string ExportJobId { get; set; } = null!;

    public int StatusCode { get; set; }

    public DateTime ExportCreated { get; set; }

    public DateTime? ExportStarted { get; set; }

    public DateTime? ExportFinished { get; set; }

    public long FileSize { get; set; }

    public string? ExportInfo { get; set; }

    public string? Doi { get; set; }
}

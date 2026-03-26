using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class CommandLog : BaseEntity
{
    public string? DatabaseName { get; set; }

    public string? SchemaName { get; set; }

    public string? ObjectName { get; set; }

    public string? ObjectType { get; set; }

    public string? IndexName { get; set; }

    public byte? IndexType { get; set; }

    public string? StatisticsName { get; set; }

    public int? PartitionNumber { get; set; }

    public string? ExtendedInfo { get; set; }

    public string Command { get; set; } = null!;

    public string CommandType { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? ErrorNumber { get; set; }

    public string? ErrorMessage { get; set; }
}

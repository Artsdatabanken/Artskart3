using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ImportLog : BaseEntity
{
    public DateTime Timestamp { get; set; }

    public DateTime TimestampFinished { get; set; }

    public double Runtime { get; set; }

    public int SumRecordsProcessed { get; set; }

    public int SumNewRecordsImported { get; set; }

    public int SumUpdatedRecords { get; set; }

    public int SumRecordsRejected { get; set; }

    public int SumRecordsDeleted { get; set; }

    public string SerializedObject { get; set; } = null!;

    public int ModeCommand { get; set; }

    public int ModeProcessMode { get; set; }

    public int ModePartitionYear { get; set; }

    public string? ModeFilePath { get; set; }

    public string? ModeDataset { get; set; }

    public bool ModeSensitiveRecords { get; set; }
}

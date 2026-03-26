using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ProcessSourceDataResult : BaseEntity
{
    public int SourceDataId { get; set; }

    public DateTime TimeStampStarted { get; set; }

    public DateTime TimeStampFinished { get; set; }

    public int SingleDatabaseResult { get; set; }

    public string? ErrorMessage { get; set; }

    public int ProcessedRecordsCount { get; set; }

    public int NewImportedRecordsCount { get; set; }

    public int RejectedRecordsCount { get; set; }

    public int WarningRecordsCount { get; set; }

    public int DeletedRecordsCount { get; set; }

    public string? SerializedObject { get; set; }
}

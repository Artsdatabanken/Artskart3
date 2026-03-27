using System;
using System.Collections.Generic;
using Artskart3.Core.Domain.Entities.Base;

namespace Artskart3.Core.Domain.Entities;

public partial class ImportState : BaseEntity
{
    public string? Name { get; set; }

    public int UpdateMode { get; set; }

    public DateTime ReportedLastTrackDateInCache { get; set; }

    public int ReportedTotalNumberOfRecordsInCache { get; set; }

    public int ImportedNumberOfRecords { get; set; }

    public int RejectedNumberOfRecords { get; set; }

    public DateTime DateLastProcessed { get; set; }

    public DateTime DateLastProcessedWithChangeInRecords { get; set; }

    public DateTime DateLastProcessedFinished { get; set; }

    public DateTime TrackDateOfLastRecordProcessed { get; set; }

    public DateTime LastRecordDateLastModified { get; set; }

    public string? LastRecordImportedCatalogNumber { get; set; }

    public string? LastProcessResult { get; set; }

    public int? FailCount { get; set; }

    public string? Doi { get; set; }

    public Guid GbifDataSetId { get; set; }
}

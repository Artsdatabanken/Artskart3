using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

public class TaxonAssessmentSyncRun : BaseEntity
{
    public int CategoryTypeId { get; set; }

    public TaxonAssessmentSyncStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int CurrentPageCount { get; set; }

    public int CurrentAssessmentCount { get; set; }

    public int SnapshotAssessmentCount { get; set; }

    public int AppliedAssessmentCount { get; set; }

    public int RetainedAssessmentCount { get; set; }

    public int SkippedAssessmentCount { get; set; }

    public string? ErrorMessage { get; set; }

    public virtual CategoryType CategoryType { get; set; } = null!;
}

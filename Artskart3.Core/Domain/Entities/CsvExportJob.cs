using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

public class CsvExportJob : BaseEntity
{
    public string UserId { get; set; } = null!;

    public CsvExportStatus Status { get; set; } = CsvExportStatus.Pending;

    /// <summary>
    /// Serialisert ObservationSearchFilterDto (JSON)
    /// </summary>
    public string FilterJson { get; set; } = null!;

    /// <summary>
    /// Serialisert liste med valgte kolonner (JSON)
    /// </summary>
    public string SelectedColumns { get; set; } = null!;

    public int TotalRows { get; set; }

    public int RowsProcessed { get; set; }

    public string? BlobPath { get; set; }

    public long FileSize { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? ErrorMessage { get; set; }
}

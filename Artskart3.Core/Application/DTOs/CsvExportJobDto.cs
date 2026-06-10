using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.DTOs;

public class CsvExportJobDto
{
    public int Id { get; set; }
    public CsvExportStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int RowsProcessed { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

namespace Artskart3.Core.Application.DTOs;

public class ExportSummaryDto
{
    public int TotalRows { get; set; }
    public long EstimatedFileSizeBytes { get; set; }
    public bool ExceedsSoftLimit { get; set; }
    public bool ExceedsHardLimit { get; set; }
}

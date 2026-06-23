using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ICsvExportService
{
    Task<IReadOnlyList<ExportColumnDefinition>> GetAvailableColumnsAsync();
    Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns);
    Task<int> StartExportAsync(string userId, ObservationSearchFilterDto filter, List<string> columns);
    Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, string userId);
    Task<bool> CancelExportAsync(int jobId, string userId);
    Task<string?> GetCsvBlobPathAsync(int jobId, string userId);
    Task<string?> GetExcelBlobPathAsync(int jobId, string userId);
    Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(string userId);
}

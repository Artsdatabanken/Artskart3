using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IExportService
{
    Task<IReadOnlyList<ExportColumnDefinition>> GetAvailableColumnsAsync();
    Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken = default);
    Task<int> StartExportAsync(string userId, ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken = default);
    Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, string userId, CancellationToken cancellationToken = default);
    Task<bool> CancelExportAsync(int jobId, string userId, CancellationToken cancellationToken = default);
    Task<string?> GetCsvBlobPathAsync(int jobId, string userId, CancellationToken cancellationToken = default);
    Task<string?> GetExcelBlobPathAsync(int jobId, string userId, CancellationToken cancellationToken = default);
    Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(string userId, CancellationToken cancellationToken = default);
}

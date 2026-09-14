using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IExportService
{
    Task<IReadOnlyList<ExportColumnDefinition>> GetAvailableColumnsAsync();
    Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken = default);
    Task<int> StartExportAsync(Guid userId, ObservationSearchFilterDto filter, List<string> columns, string? name, CancellationToken cancellationToken = default);
    Task<CsvExportJobDto?> GetJobStatusAsync(int jobId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CancelExportAsync(int jobId, Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetCsvBlobPathAsync(int jobId, Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetExcelBlobPathAsync(int jobId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
}

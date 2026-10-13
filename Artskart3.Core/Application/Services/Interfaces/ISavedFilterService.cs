using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ISavedFilterService
{
    Task<List<SavedFilterDto>> GetUserFiltersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SavedFilterDto> CreateAsync(Guid userId, CreateSavedFilterRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid publicId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultAsync(Guid publicId, Guid userId, bool isDefault, CancellationToken cancellationToken = default);
}

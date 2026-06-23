using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface ISpeciesService
{
    Task<List<SpeciesDto>> SearchSpeciesAsync(string searchInput, CancellationToken cancellationToken = default);
}

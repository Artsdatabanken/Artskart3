using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IPlaceSearchService
{
    Task<List<PlaceSearchResultDto>> SearchPlacesAsync(string searchInput, CancellationToken cancellationToken = default);
}

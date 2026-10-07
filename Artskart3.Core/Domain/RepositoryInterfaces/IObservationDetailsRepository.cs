using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Domain.RepositoryInterfaces;

public interface IObservationDetailsRepository
{
    Task<ObservationDetailDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<ObservationImageFile?> GetImageAsync(int observationId, int mediaId, CancellationToken cancellationToken);
}

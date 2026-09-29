using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationDetailsService
{
    Task<ObservationDetailDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<ObservationImageFile?> GetImageAsync(int observationId, int mediaId, CancellationToken cancellationToken);
}

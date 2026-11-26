using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Annoteringene som gjelder et funn, hentet fra annoteringstjenesten i Artskart 2.
/// Databasen vår har bare flaggene annoteringene satte, ikke kommentarene.
/// </summary>
public interface IObservationAnnotationService
{
    /// <summary>
    /// Null når funnet ikke finnes. Kaster HttpRequestException, JsonException eller
    /// TaskCanceledException (tidsavbrudd) når tjenesten feiler.
    /// </summary>
    Task<List<ObservationAnnotationDto>?> GetAsync(int observationId, CancellationToken cancellationToken);
}

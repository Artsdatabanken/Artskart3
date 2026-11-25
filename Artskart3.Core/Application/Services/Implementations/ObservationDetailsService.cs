using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Application.Validation;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.Extensions.Logging;

namespace Artskart3.Core.Application.Services.Implementations;

public sealed class ObservationDetailsService(
    IObservationDetailsRepository repository,
    ILogger<ObservationDetailsService> logger) : IObservationDetailsService
{
    public async Task<ObservationDetailDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var detail = await repository.GetAsync(id, cancellationToken);
        if (detail is null) return null;

        if (detail.Point is { } point && !ObservationPointValidator.IsUsable(point))
        {
            logger.LogWarning("Observation {ObservationId} has no usable public coordinate", id);
            detail.Point = null;
        }

        foreach (var image in detail.Images)
        {
            if (image.Origin is not null && !WebUrl.IsValid(image.Origin))
            {
                logger.LogWarning("Observation {ObservationId}, media {MediaId} has an invalid origin", id, image.Id);
                image.Origin = null;
            }
        }
        if (detail.AssessmentUrl is not null && !WebUrl.IsValid(detail.AssessmentUrl))
        {
            logger.LogWarning("Observation {ObservationId} has an invalid assessment URL", id);
            detail.AssessmentUrl = null;
        }
        return detail;
    }

    public async Task<ObservationMapSubject?> GetMapSubjectAsync(int id, CancellationToken cancellationToken)
    {
        var location = await repository.GetMapLocationAsync(id, cancellationToken);
        if (location is null) return null;

        var shape = location is { Geometry: { } geometry, GeometryType: { } type }
            && LocationGeometryRules.TegnedeGeometrityper.Contains(type)
            && LocationGeometryRules.TegnesSomGeometri(geometry.AsText(), location.LookupId)
                ? geometry : null;
        return new ObservationMapSubject(
            ObservationPointValidator.UsableOrNull(location.Point),
            location.CoordinatePrecision,
            shape,
            shape is not null && LocationGeometryRules.ErSensitivLokalitet(location.LookupId));
    }

    public Task<int?> FindIdByOccurrenceIdAsync(string occurrenceId, CancellationToken cancellationToken) =>
        repository.FindIdByOccurrenceIdAsync(occurrenceId, cancellationToken);

    public Task<ObservationImageFile?> GetImageAsync(int observationId, int mediaId, CancellationToken cancellationToken) =>
        repository.GetImageAsync(observationId, mediaId, cancellationToken);
}

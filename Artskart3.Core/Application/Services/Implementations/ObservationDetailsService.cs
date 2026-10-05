using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
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

        if (detail.Point is { } point &&
            (!double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) ||
             point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180 ||
             (point.Latitude == 0 && point.Longitude == 0) || point.North <= 0))
        {
            logger.LogWarning("Observation {ObservationId} has no usable public coordinate", id);
            detail.Point = null;
        }

        foreach (var image in detail.Images)
        {
            if (image.Origin is not null && !IsWebUrl(image.Origin))
            {
                logger.LogWarning("Observation {ObservationId}, media {MediaId} has an invalid origin", id, image.Id);
                image.Origin = null;
            }
        }
        if (detail.AssessmentUrl is not null && !IsWebUrl(detail.AssessmentUrl))
        {
            logger.LogWarning("Observation {ObservationId} has an invalid assessment URL", id);
            detail.AssessmentUrl = null;
        }
        return detail;
    }

    public Task<ObservationImageFile?> GetImageAsync(int observationId, int mediaId, CancellationToken cancellationToken) =>
        repository.GetImageAsync(observationId, mediaId, cancellationToken);

    private static bool IsWebUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo);
}

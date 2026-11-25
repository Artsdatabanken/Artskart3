using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

public interface IObservationMapService
{
    /// <summary>
    /// Endres når bildet ville blitt et annet. Brukes som ETag, så en nettleser med
    /// gjeldende bilde får 304 uten at noe tegnes.
    /// </summary>
    string Fingerprint(ObservationMapSubject subject, bool overview);

    /// <exception cref="ObservationMapBusyException">For mange kart tegnes allerede.</exception>
    Task<ObservationImageFile> RenderAsync(ObservationMapSubject subject, bool overview, CancellationToken cancellationToken);
}

public sealed class ObservationMapBusyException() : Exception("Too many observation maps are being rendered.");

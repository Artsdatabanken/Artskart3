namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Resultatet av å tolke en MGRS-rutereferanse: punktet i ETRS89 / UTM for sonen
/// referansen oppgir, og presisjonen rutestørrelsen gir.
/// </summary>
/// <param name="Point">Punktet, merket med UTM-sonens EPSG-kode.</param>
/// <param name="CoordinatePrecisionInMeters">
/// Presisjonen MGRS-rutens størrelse tilsier. Brukes som
/// <c>Observation.CoordinatePrecisionInMeters</c> når
/// <c>CoordinateUncertaintyInMeters</c> mangler, og gir da merknaden
/// <see cref="ImportIssueRemark.CoordinateUncertaintySetFromMgrsCoordinate"/>.
/// </param>
public sealed record MgrsCoordinate(GeoPoint Point, int? CoordinatePrecisionInMeters);

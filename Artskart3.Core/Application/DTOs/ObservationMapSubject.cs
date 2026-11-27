using Artskart3.Core.Domain.Enums;
using NetTopologySuite.Geometries;

namespace Artskart3.Core.Application.DTOs;

/// <summary>Det kartbildene til en observasjon tegnes ut fra.</summary>
/// <param name="Point">Det offentlige punktet, eller null når det ikke er brukbart.</param>
/// <param name="CoordinatePrecision">Presisjonen i meter.</param>
/// <param name="Shape">Lokasjonens flate eller linje når kartet tegner den, ellers null.</param>
/// <param name="ShapeIsMaskingSquare">Formen er maskeringsruta til et sensitivt funn, ikke funnets utstrekning.</param>
public sealed record ObservationMapSubject(
    ObservationPointDto? Point, int? CoordinatePrecision, Geometry? Shape, bool ShapeIsMaskingSquare);

/// <summary>Kartdataene til en observasjon slik de ligger i databasen.</summary>
public sealed record ObservationMapLocation(
    ObservationPointDto Point, int? CoordinatePrecision, LocationGeometryType? GeometryType, Geometry? Geometry, string? LookupId);

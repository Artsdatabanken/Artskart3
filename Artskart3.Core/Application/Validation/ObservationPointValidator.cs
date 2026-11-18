using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Validation;

public static class ObservationPointValidator
{
    public static bool IsUsable(ObservationPointDto point) =>
        double.IsFinite(point.Latitude) && double.IsFinite(point.Longitude) &&
        point.Latitude is >= -90 and <= 90 && point.Longitude is >= -180 and <= 180 &&
        !(point.Latitude == 0 && point.Longitude == 0) && point.North > 0;
}

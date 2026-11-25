namespace Artskart3.Core.Application.DTOs;

public class CustomAreaLookupRequestDto
{
    /// <summary>
    /// Polygonet som WKT i EPSG:25833, som <see cref="IObservationFilter.CustomAreaWkt"/>.
    /// </summary>
    public string Wkt { get; set; } = null!;

    /// <summary>
    /// Resten av filteret. Med andre områdevalg deles ikke polygonet, og da må hele det holde
    /// seg under grensen, se ObservationFilterValidator.CanSplitCustomArea.
    /// </summary>
    public LocationSearchFilterDto? Filter { get; set; }
}

/// <summary>
/// Om et egendefinert område kan brukes som filter.
/// </summary>
public class CustomAreaLookupDto
{
    public bool TooManyLocations { get; set; }

    public int MaxLocationCount { get; set; }
}

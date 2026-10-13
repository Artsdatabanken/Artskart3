namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// MeasurementOrFact-utvidelsen på en cache-post. Port av
/// <c>DarwinCoreProxy.MeasurementOrFact</c>.
/// </summary>
public class CacheMeasurementOrFact
{
    public string? MeasurementType { get; set; }

    public string? MeasurementValue { get; set; }

    public string? MeasurementUnit { get; set; }
}

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Extended MeasurementOrFact på en cache-post. Port av
/// <c>DarwinCoreProxy.ExtendedMeasurementOrFact</c>, som arver
/// <c>MeasurementOrFact</c> — derfor ligger <c>MeasurementType</c>,
/// <c>MeasurementValue</c> og <c>MeasurementUnit</c> i basisklassen.
/// </summary>
public class CacheExtendedMeasurementOrFact : CacheMeasurementOrFact
{
    public string? OccurrenceID { get; set; }

    public string? MeasurementTypeID { get; set; }

    public string? MeasurementValueID { get; set; }

    public string? MeasurementAccuracy { get; set; }

    public string? MeasurementUnitID { get; set; }

    public string? MeasurementDeterminedDate { get; set; }

    public string? MeasurementDeterminedBy { get; set; }

    public string? MeasurementMethod { get; set; }

    public string? MeasurementRemarks { get; set; }

    public string? MeasurementId { get; set; }
}

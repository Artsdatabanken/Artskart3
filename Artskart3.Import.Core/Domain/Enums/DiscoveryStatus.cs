namespace Artskart3.Import.Core.Domain.Enums;

public enum DiscoveryStatus
{
    /// <summary>
    /// Discovered via the GBIF API but not yet reviewed.
    /// </summary>
    Pending,

    /// <summary>
    /// Reviewed and approved for import as a DataSource.
    /// </summary>
    Approved,

    /// <summary>
    /// Reviewed and determined to be not relevant for import.
    /// </summary>
    Rejected
}

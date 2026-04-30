using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Provider configuration for a remote data source (formerly SourceDataBase in RavenDB).
/// One flat table; use typed views or application-layer filtering to work with specific provider types.
/// </summary>
public class DataSource : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Discriminates the provider type and drives import pipeline branching.
    /// </summary>
    public ProviderType ProviderType { get; set; }

    /// <summary>
    /// How many records to fetch per batch during harvest.
    /// </summary>
    public int RecordsBatchSize { get; set; }

    /// <summary>
    /// Web service API version (ArtskartDataDeliveryWebServiceV1 providers).
    /// </summary>
    public int WebServiceVersion { get; set; }

    /// <summary>
    /// URL of the remote endpoint (web service or IPT archive base URL).
    /// </summary>
    public string? RemoteAddress { get; set; }

    /// <summary>
    /// IPT archive filename, or local cache filename for GbifApi providers.
    /// </summary>
    public string? ArchiveName { get; set; }

    /// <summary>
    /// Internal notes for operators.
    /// </summary>
    public string? Notes { get; set; }

    public bool IsEditedNameOrNotes { get; set; }

    /// <summary>
    /// Flags records from this source as having non-valid occurrence IDs.
    /// </summary>
    public bool NonValidOccurrenceIds { get; set; }

    /// <summary>
    /// JSON predicate string used by GbifApi providers to query the GBIF occurrence API.
    /// </summary>
    public string? GbifApiQuery { get; set; }

    /// <summary>
    /// JSON overrides applied during the harvest phase (field mapping / transformations).
    /// </summary>
    public string? HarvestPropertyOverrides { get; set; }

    /// <summary>
    /// JSON overrides applied during the import/processing phase.
    /// </summary>
    public string? ImportPropertyOverrides { get; set; }

    /// <summary>
    /// How often (in days) the source should be re-harvested.
    /// </summary>
    public int UpdateFrequencyInDays { get; set; }

    /// <summary>
    /// Digital Object Identifier — replaces GbifDataSetId from the old solution.
    /// Required for active GbifIpt and GbifApi providers; enforced at the application layer.
    /// </summary>
    public string? Doi { get; set; }

    /// <summary>
    /// GBIF publisher UUID. Used for API-based providers and publisher-scoped queries.
    /// </summary>
    public Guid? GbifPublisherId { get; set; }

    /// <summary>
    /// Marks observations from this source as sensitive (restricted coordinates etc.).
    /// </summary>
    public bool IsSensitive { get; set; }
}

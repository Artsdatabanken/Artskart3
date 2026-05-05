using Artskart3.Import.Core.Domain.Enums;

namespace Artskart3.Import.Core.Domain.Entities;

public class GbifDatasetDiscovery
{
    public int Id { get; set; }

    /// <summary>
    /// The GBIF dataset key (UUID).
    /// </summary>
    public string GbifId { get; set; } = string.Empty;

    /// <summary>
    /// The GBIF publisher/organisation UUID.
    /// </summary>
    public Guid GbifPublisherId { get; set; }

    /// <summary>
    /// The GBIF publishing organisation key string.
    /// </summary>
    public string PublishingOrganizationKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// The dataset homepage URL on GBIF.
    /// </summary>
    public string? HomepageUrl { get; set; }

    /// <summary>
    /// The Darwin Core Archive download URL.
    /// </summary>
    public string? DwcArchiveUrl { get; set; }

    public string? Doi { get; set; }

    /// <summary>
    /// Additional identifiers for the dataset (stored as JSON array).
    /// </summary>
    public List<string> Identifiers { get; set; } = [];

    /// <summary>
    /// Review status of this discovered dataset.
    /// </summary>
    public DiscoveryStatus Status { get; set; } = DiscoveryStatus.Pending;

    /// <summary>
    /// When this dataset was first discovered via the GBIF API.
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

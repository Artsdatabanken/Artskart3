namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// En lokasjonsliste lagret i databasen, så spørringene kan slå den opp i en tabell i stedet for
/// å få hele listen som én JSON-parameter. Lister med samme innhold deler én rad.
/// </summary>
public class LocationList
{
    public int Id { get; set; }

    /// <summary>SHA-256 av de sorterte lokasjons-ID-ene.</summary>
    public byte[] Hash { get; set; } = [];

    public int LocationCount { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Settes hver gang en instans tar listen i bruk. Lister som ikke er brukt på et døgn, ryddes bort.</summary>
    public DateTime LastUsedAt { get; set; }
}

public class LocationListEntry
{
    public int ListId { get; set; }

    public int LocationId { get; set; }
}

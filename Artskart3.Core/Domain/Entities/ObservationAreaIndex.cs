namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Denormalisert indekstabell for raske observasjon-til-entitet-oppslag.
/// EntityTypeId angir hvilken type entitet raden refererer til (se ObservationIndexEntityType).
/// EntityId er den numeriske IDen til entiteten (Area.Fid konvertert til int, eller Organization.Id).
/// </summary>
public class ObservationEntityIndex
{
    public int ObservationId { get; set; }
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
}

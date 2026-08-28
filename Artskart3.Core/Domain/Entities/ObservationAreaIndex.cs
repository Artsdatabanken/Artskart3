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

    // Denormaliserte observasjonsattributter for rask filtrert telling
    public int TaxonGroupId { get; set; }
    public int? CategoryId { get; set; }
    public int BasisOfRecordId { get; set; }
    public int? CoordinatePrecisionInMeters { get; set; }
    public DateTime? DateTimeCollected { get; set; }
    public byte RegistrationStatusId { get; set; }
    public bool HasMediaFiles { get; set; }
    public int? SpeciesTaxonId { get; set; }
    public int? GenusTaxonId { get; set; }
    public int? FamilyTaxonId { get; set; }
    public int? OrderTaxonId { get; set; }
}

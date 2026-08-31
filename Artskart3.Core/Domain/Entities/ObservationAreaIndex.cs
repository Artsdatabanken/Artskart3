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

    // CompleteFilter — lavselektive filtre som hører hjemme i columnstore.
    // Institusjon har 54 distinkte verdier (~1,13M rader per verdi), samling 1 908
    // (~32k), atferd 6. For alle tre ligger kostnaden i aggregeringen, ikke i å finne
    // radene, så de skal IKKE ha rowstore-indekser. Se kommentaren i DbContext.
    //
    // Datasett er bevisst ikke her — det er ikke 1:1 og ligger i ObservationDataset.
    // Katalognummer er heller ikke her — det er tilnærmet unikt, og løses med seek
    // på ObservationId fra typeahead-endepunktet.
    public int? InstitutionOrgId { get; set; }
    public int? CollectionOrgId { get; set; }
    public byte? BehaviorId { get; set; }
}

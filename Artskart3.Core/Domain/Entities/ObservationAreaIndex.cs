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

    /// <summary>
    /// Denormalisert fra Observation.LocationId.
    ///
    /// Uten den måtte lokasjonssøket joine tilbake til Observation bare for å få
    /// grupperingsnøkkelen, og aggregeringen falt ut av batch mode. Målt på
    /// Oslo-utsnittet: 11 166 ms via Observation, 5116 ms via indekstabellen med
    /// join, og 1415 ms for ren columnstore-aggregering med samme antall grupper.
    ///
    /// Kolonnen endres praktisk talt aldri etter import — en observasjon bytter
    /// ikke lokalitet — så vedlikeholdskostnaden er lav.
    /// </summary>
    public int? LocationId { get; set; }
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
    // Datasett er bevisst ikke her — det er ikke 1:1 og ligger i ObservationProject.
    // Katalognummer er heller ikke her — det er tilnærmet unikt, og løses med seek
    // på ObservationId fra typeahead-endepunktet.
    public int? InstitutionOrgId { get; set; }
    public int? DatasetOrgId { get; set; }
    public byte? BehaviorId { get; set; }
}

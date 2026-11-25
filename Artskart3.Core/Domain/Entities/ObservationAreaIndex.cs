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
    /// Innsamlingsmåned, denormalisert fra Observation.
    ///
    /// Måneden ligger implisitt i DateTimeCollected, men DATEPART(month, ...) er
    /// ikke sargbart: columnstore må da levere alle 133 mill. rader ut av
    /// scanningen og beregne funksjonen per rad før filteret slår til. Målt på
    /// Oslo-utsnittet 4271 ms mot 1550 for et sargbart datointervall — og det
    /// til tross for at månedsfilteret slipper gjennom FÆRRE rader (50,3 mot
    /// 126,6 mill.). Estimatet bommet 40x, siden optimizeren ikke kan estimere
    /// en funksjon på en kolonne.
    ///
    /// Verdien er NULL nøyaktig når DateTimeCollected er NULL (958 912 rader),
    /// så kolonnen gir samme svar som før — verifisert til 22 688 357 treff for
    /// sommermånedene med begge formene. Dette er en ren ytelsesendring.
    /// </summary>
    public byte? MonthCollected { get; set; }

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
    // Taksonrangkolonner — én per rang, denormalisert fra ObservationTaxonHierarchy
    // av BackfillAll seksjon D. Alle 23 rangene som forekommer som synlige noder i
    // artstreet ligger her.
    //
    // HVORFOR ALLE OG IKKE BARE FIRE
    // Fram til nå hadde indekstabellen bare art, slekt, familie og orden, og alt
    // annet falt tilbake til ObservationTaxonHierarchy. Det ga to ting:
    // taksonfilteret hadde to kodestier, og artstreet kunne ikke vise filtrerte
    // tall for 9 % av nodene uten et sekundoppslag per utvidelse.
    //
    // Målt kostnad for de 19 nye: +2,37 GB i rowstore (sidekomprimert) og +65 MB i
    // columnstore. De komprimerer nær bort fordi de enten er svært repetitive
    // (272 klasser over 61M rader) eller nesten tomme (varietet 0,1 %).
    //
    // MERK 2,2x-REDUNDANSEN. Tabellen har ~2,2 rader per observasjon, én per
    // områdetype, så hver rangkolonne lagres 2,2 ganger. Det er prisen for at
    // filteret og grupperingen ligger på samme rad som områdetilknytningen.
    // ObservationTaxonHierarchy beholdes: den har én rad per observasjon og bærer
    // taksonfilteret mot Observation-tabellen, der et seek på IX_OTH_* er målt 4,4x
    // raskere enn DISTINCT over indekstabellen.
    //
    // ALLE MÅ MED I IX_OEI_Columnstore, se BackfillAll nederst. En kolonne utenfor
    // den filtreres fra rowstore og er treg.
    public int? KingdomTaxonId { get; set; }
    public int? SubkingdomTaxonId { get; set; }
    public int? PhylumTaxonId { get; set; }
    public int? SubphylumTaxonId { get; set; }
    public int? SuperclassTaxonId { get; set; }
    public int? ClassTaxonId { get; set; }
    public int? SubclassTaxonId { get; set; }
    public int? InfraclassTaxonId { get; set; }
    public int? SuperorderTaxonId { get; set; }
    public int? OrderTaxonId { get; set; }
    public int? SuborderTaxonId { get; set; }
    public int? InfraorderTaxonId { get; set; }
    public int? SuperfamilyTaxonId { get; set; }
    public int? FamilyTaxonId { get; set; }
    public int? SubfamilyTaxonId { get; set; }
    public int? TribeTaxonId { get; set; }
    public int? SubgenusTaxonId { get; set; }
    public int? GenusTaxonId { get; set; }
    public int? SectionTaxonId { get; set; }
    public int? SpeciesTaxonId { get; set; }
    public int? SubspeciesTaxonId { get; set; }
    public int? VarietyTaxonId { get; set; }
    public int? FormTaxonId { get; set; }

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

    /// <summary>
    /// Denormalisert fra taggen AnnotatedChanges, som kolonnen med samme navn på
    /// Observation. Her for at områdetellingene skal slippe semi-joinen mot
    /// ObservationTags: den kostet 759-1000 ms på negasjonen, der alt annet
    /// columnstore-filter ligger under 10 ms.
    ///
    /// Som de andre lavselektive filterkolonnene: ingen rowstore-indeks, den
    /// betjenes av IX_OEI_Columnstore.
    /// </summary>
    public bool ChangedByAnnotation { get; set; }

    /// <summary>Denormalisert fra taggen Blocked. Se <see cref="ChangedByAnnotation"/>.</summary>
    public bool HiddenByAnnotation { get; set; }

    /// <summary>Denormalisert fra taggen Sensitive. Se <see cref="ChangedByAnnotation"/>.</summary>
    public bool Sensitive { get; set; }

    /// <summary>Denormalisert fra taggen UnsureIdentification. Se <see cref="ChangedByAnnotation"/>.</summary>
    public bool UncertainIdentification { get; set; }

    /// <summary>Denormalisert fra taggen Unspontaneus. Se <see cref="ChangedByAnnotation"/>.</summary>
    public bool Introduced { get; set; }
}

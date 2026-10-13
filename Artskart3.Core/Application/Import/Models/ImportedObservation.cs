using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Én observasjon under bygging, i minnet. Motsvarer Artskart 2s <c>Observation</c>
/// slik den ser ut mens importtaskene jobber på den — før noe skrives.
///
/// EN REN MODELL, IKKE EN ENTITET. Den speiler <c>Observation</c>,
/// <c>ObservationDetail</c> og barnetabellene, men er ikke sporet av EF. Fase 6
/// oversetter den til staging-tabeller; derfor er det heller ingen navigasjons-
/// egenskaper her, bare ID-er og lister.
///
/// FELT SOM IKKE FINNES I ARTSKART 3s SKJEMA LENGER ER LIKEVEL MED:
/// <see cref="InstitutionCode"/> og <see cref="CollectionCode"/> ble fjernet fra
/// <c>Observation</c> av <c>CleanupCompleteFilterSchema</c>, men importen trenger dem
/// underveis — oppslaget går på kode, og en avvist post lagrer kodene i
/// <c>RejectedRecord</c>.
///
/// FELT SOM BEREGNES AV DATABASEN SKAL IKKE SETTES: <c>YearCollected</c> og
/// <c>MonthCollected</c> er beregnede kolonner (<c>datepart</c> av
/// <see cref="DateTimeCollected"/>) i Artskart 3, og finnes derfor ikke her.
/// </summary>
public class ImportedObservation
{
    // --- Identitet og opphav ------------------------------------------------

    /// <summary>
    /// Forretningsnøkkelen, satt av <c>DarwinCoreHelper.CleanDarwinCoreId</c>. Dette er
    /// det som binder posten til en eventuelt eksisterende observasjon.
    /// </summary>
    public string? ProxyId { get; set; }

    public string? OccurrenceId { get; set; }

    public string? CatalogNumber { get; set; }

    public int NodeId { get; set; }

    /// <summary>Institusjonens kanoniske kode, ikke kildens. Brukes til oppslag og i avviste poster.</summary>
    public string? InstitutionCode { get; set; }

    /// <summary>Samlingens kode. Tom verdi i kilden settes lik institusjonskoden.</summary>
    public string? CollectionCode { get; set; }

    /// <summary><c>Organization.Id</c> for type 1 (Publisher).</summary>
    public int? InstitutionOrgId { get; set; }

    /// <summary><c>Organization.Id</c> for type 2 (Dataset).</summary>
    public int? DatasetOrgId { get; set; }

    /// <summary>
    /// <c>Organization.Id</c> for type 3 (Project), én rad per prosjekt i
    /// <c>ObservationProject</c>. Artskart 2 skrev disse som
    /// <c>OrganizationRelation</c>-rader; tabellen finnes ikke lenger.
    /// </summary>
    public List<int> ProjectOrgIds { get; } = [];

    // --- Klassifisering ----------------------------------------------------

    public int BasisOfRecordId { get; set; }

    /// <summary>
    /// Atferd. Artskart 2 tillater flere per observasjon, men det er målt 1:1 — og
    /// <c>Observation.BehaviorId</c> i Artskart 3 har bare plass til én. Listen bærer
    /// alle, kolonnen får den som velges. Hva som skal skje når en post har to er et
    /// åpent spørsmål (plan 7.2 punkt 6).
    /// </summary>
    public List<byte> BehaviorIds { get; } = [];

    public int? CategoryId { get; set; }

    public int? ObservationQualityTypeId { get; set; }

    /// <summary>1 = funnet, 2 = <c>Tag.Absent</c>, 3 = <c>Tag.NotRecovered</c>.</summary>
    public byte RegistrationStatusId { get; set; } = 1;

    public HashSet<Tag> Tags { get; } = [];

    // --- Taksonomi ----------------------------------------------------------

    public int TaxonId { get; set; }

    public int MatchedScientificNameId { get; set; }

    public int TaxonGroupId { get; set; }

    /// <summary>Hele <c>TaxonIdHiarchy</c> for taksonet — én rad per nivå i <c>ObservationTaxons</c>.</summary>
    public List<int> TaxonIds { get; } = [];

    // --- Posisjon -----------------------------------------------------------

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int East { get; set; }

    public int North { get; set; }

    public int? CoordinatePrecisionInMeters { get; set; }

    /// <summary>
    /// Lokaliteten posten hører til. Deles på tvers av observasjoner og skrives først
    /// — <c>LocationId</c> settes når den er lagret (fase 6).
    /// </summary>
    public ImportedLocation? Location { get; set; }

    public int? LocationId { get; set; }

    // --- Datoer -------------------------------------------------------------

    public DateTime DateLastModified { get; set; }

    public DateTime? DateTimeCollected { get; set; }

    public DateTime? DatetimeIdentified { get; set; }

    public DateTime DateTimeRecordImported { get; set; }

    public DateTime DateTimeRecordProcessed { get; set; }

    // --- Tilstand -----------------------------------------------------------

    /// <summary>Hashen fra cache-kolonnen, båret uendret gjennom importen.</summary>
    public int HashCode { get; set; }

    public int ProcessEngineId { get; set; }

    public bool HasErrors { get; set; }

    public bool HasAnnotations { get; set; }

    /// <summary>
    /// Denormalisert kolonne på <c>Observation</c> i Artskart 3. Utledet og ikke
    /// settbar: kommentaren i <c>Observation.cs</c> advarer om at kolonnene må
    /// oppdateres der kildene endres, ellers driver de stille fra hverandre og
    /// filtrene svarer feil uten at noe feiler. Da er det tryggere at den ikke kan
    /// settes feil — særlig fordi maskeringen i fase 8 kan komme til å fjerne
    /// mediefiler.
    /// </summary>
    public bool HasMediaFiles => MediaFiles.Count > 0;

    // --- ObservationDetail --------------------------------------------------

    public string? DatasetId { get; set; }

    public string? DatasetName { get; set; }

    public string? Collector { get; set; }

    public string? IndividualCount { get; set; }

    /// <summary>
    /// Fritekst flere steg skriver til. <c>ImportSimpleProperties</c> kopierer
    /// <c>record.Notes</c> hit, og <c>ImportCheckValidDates</c> legger til etterpå.
    /// </summary>
    public string? Notes { get; set; }

    public string? IdentifiedBy { get; set; }

    public string? Locality { get; set; }

    public string? Habitat { get; set; }

    public string? Sex { get; set; }

    public string? DateTimeCollectedStr { get; set; }

    /// <summary>Lagres som én <c>"|"</c>-joinet streng, og kolonnen er <c>NOT NULL</c>.</summary>
    public List<string> AssociatedReferences { get; } = [];

    public string? CollectingMethod { get; set; }

    public string? RecordNumber { get; set; }

    public string? FieldNumber { get; set; }

    public string? MeasurementMethod { get; set; }

    public string? GeoreferenceRemarks { get; set; }

    public string? Preparations { get; set; }

    public string? OtherCatalogNumbers { get; set; }

    public string? RelatedResourceId { get; set; }

    public string? RelationshipOfResource { get; set; }

    public string? TypeStatus { get; set; }

    public string? EventTime { get; set; }

    public int? MaximumElevationInMeters { get; set; }

    public int? MinimumElevationInMeters { get; set; }

    public string? VerbatimDepth { get; set; }

    /// <summary>
    /// Satt helt til slutt, av kjeden: ordboken i <see cref="ImportContext"/>, sortert
    /// på nøkkel og serialisert til JSON.
    /// </summary>
    public string? DynamicProperties { get; set; }

    // --- Barnetabeller ------------------------------------------------------

    public List<ImportedObservationError> Errors { get; } = [];

    public List<ImportedMediaFile> MediaFiles { get; } = [];

    public List<ImportedObservationLink> Links { get; } = [];

    /// <summary>De umaskerte verdiene for en sensitiv observasjon.</summary>
    public ImportedSensitiveData? SensitiveData { get; set; }

    // --- Hjelpere stegene bruker -------------------------------------------

    /// <summary>
    /// Legger til en referanse hvis den ikke står der fra før. Artskart 2 bruker
    /// <c>Union</c>, som er ordinal og skiller mellom store og små bokstaver — to
    /// URL-er som bare avviker i bokstavstørrelse blir altså to referanser.
    /// </summary>
    public void AddAssociatedReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        if (AssociatedReferences.Contains(reference, StringComparer.Ordinal)) return;

        AssociatedReferences.Add(reference);
    }

    /// <summary>
    /// Erstatter referanselisten. Finnes fordi <c>ImportSimpleProperties</c> gjør
    /// nettopp det for GBIF-nodene 40 og 211 — og dermed kaster det
    /// <c>ImportMedia</c> har lagt inn.
    /// </summary>
    public void ReplaceAssociatedReferences(params string[] references)
    {
        AssociatedReferences.Clear();
        AssociatedReferences.AddRange(references);
    }

    public void AddTag(Tag tag) => Tags.Add(tag);

    public void RemoveTag(Tag tag) => Tags.Remove(tag);

    public bool HasTag(Tag tag) => Tags.Contains(tag);

    /// <summary>
    /// Legger på en feilrad og setter <see cref="HasErrors"/>. Posten avvises ikke av
    /// dette — en observasjon med <c>HasErrors</c> importeres, men er merket.
    /// </summary>
    public void AddError(
        RecordValidationCode validationCode,
        string? errorContext = null,
        int? annotationId = null)
    {
        HasErrors = true;
        Errors.Add(new ImportedObservationError
        {
            ValidationCode = validationCode,
            ErrorContext = errorContext,
            AnnotationId = annotationId
        });
    }

    /// <summary>
    /// Føyer tekst på <see cref="Notes"/> uten skilletegn, slik Artskart 2 gjør med
    /// <c>+=</c>. Hvert kallsted bærer sin egen avstand — meldingene i
    /// <c>ImportCheckValidDates</c> har mellomrom bakerst, og de glir derfor sammen med
    /// det som sto der fra før. Det er dagens tekst i 61 millioner rader.
    ///
    /// Trunkerer ikke: feltet er <c>NVARCHAR(3500)</c>, og Artskart 2 kutter ikke her
    /// heller.
    /// </summary>
    public void AppendNotes(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        Notes += text;
    }
}

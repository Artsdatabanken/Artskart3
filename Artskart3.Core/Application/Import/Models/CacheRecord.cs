namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Én post slik den ligger i cache-databasen — Artskart 3s motstykke til Artskart 2s
/// <c>DarwinCoreProxy</c> (med basisklassen <c>EventCore</c>).
///
/// NESTEN ALLE FELT KOMMER FRA ETT JSON-DOKUMENT. Tabellen
/// <c>Artskart2Cache.DarwinCoreProxies</c> har bare en håndfull ekte kolonner; de
/// ~115 Darwin Core-feltene ligger gzip-komprimert i <c>SerializedObject</c>. Se
/// <c>CacheBlobCompression</c> for pakkingen og <c>CacheRecordSerializer</c> for
/// JSON-laget.
///
/// EGENSKAPSNAVNENE ER KILDENS, IKKE VÅRE. Blobene er serialisert med Newtonsoft og
/// medlemsnavnene slik de står i Artskart 2. Derfor heter feltene <c>MGRSNotation</c>,
/// <c>UTMnord</c>, <c>Okologi</c>, <c>NorskNavn</c> og <c>RelativeAboundance</c>
/// (skrivefeilen er original) — og derfor skal de ikke døpes om. Et nytt navn uten
/// tilhørende JSON-attributt gir et felt som stille blir stående null for 60
/// millioner poster.
///
/// TRIMMER VED TILORDNING, slik Artskart 2 gjør. Setterne der gjør
/// <c>!IsNullOrWhiteSpace(v) ? v.Trim() : null</c>, og de kjører også ved
/// deserialisering. Konsekvensen er at blanke strenger blir <c>null</c>, og det er den
/// forskjellen nedstrøms kode faktisk ser: alle <c>IsNullOrEmpty</c>-sjekkene i
/// importen oppfører seg annerledes hvis et felt kommer gjennom som " ".
/// Normaliseringen er lagt på de samme feltene som i Artskart 2 — verken flere eller
/// færre.
///
/// INGEN TOLKNING UTOVER DET. Trunkering, oppslag, datovalidering og resten av
/// tolkningene i feltmatrisen hører til importstegene, ikke hit.
/// </summary>
public class CacheRecord
{
    private static string? Normaliser(string? verdi) =>
        string.IsNullOrWhiteSpace(verdi) ? null : verdi.Trim();

    // --- Rad-metadata -------------------------------------------------------
    // Id, NodeDatabaseID, IsDeleted, DateLastModified og TrackInfoDateImported finnes
    // både som JSON-felt og som ekte kolonner. Harvest skriver begge steder, og
    // Artskart 2 leser JSON-verdien. Vi gjør det samme, så en eventuell uenighet
    // mellom blob og kolonne gir samme resultat i begge løsningene.

    /// <summary>Forretningsnøkkelen harvest genererte. Kolonnen heter <c>ProxyId</c>.</summary>
    public string? Id { get; set; }

    public int NodeDatabaseID { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>Endringstidspunkt oppgitt av kilden.</summary>
    public DateTime DateLastModified { get; set; }

    /// <summary>Satt av harvest. Dette er nøkkelen inkrementell import leser på.</summary>
    public DateTime TrackInfoDateImported { get; set; }

    /// <summary>
    /// Hash av det deserialiserte objektet. Leses fra kolonnen <c>Hash</c>, ikke fra
    /// blobben — nøyaktig som <c>SqlDarwinCoreProxy.ToDomainObject</c> gjør det.
    /// </summary>
    public int HashCode { get; set; }

    /// <summary>
    /// Primærnøkkelen til cache-raden. Finnes ikke i Artskart 2s domeneobjekt, men
    /// lesestien sorterer og sider på den, og den er det eneste holdepunktet når en
    /// post skal finnes igjen i cachen.
    /// </summary>
    public int CacheRowId { get; set; }

    // --- Identitet, institusjon, samling, datasett --------------------------

    private string? _occurrenceId;
    public string? OccurrenceID
    {
        get => _occurrenceId;
        set => _occurrenceId = Normaliser(value);
    }

    private string? _catalogNumber;
    public string? CatalogNumber
    {
        get => _catalogNumber;
        set => _catalogNumber = Normaliser(value);
    }

    private string? _institutionCode;
    public string? InstitutionCode
    {
        get => _institutionCode;
        set => _institutionCode = Normaliser(value);
    }

    private string? _collectionCode;
    public string? CollectionCode
    {
        get => _collectionCode;
        set => _collectionCode = Normaliser(value);
    }

    private string? _ownerInstitutionCode;
    public string? OwnerInstitutionCode
    {
        get => _ownerInstitutionCode;
        set => _ownerInstitutionCode = Normaliser(value);
    }

    private string? _previousCatalogNumber;
    public string? PreviousCatalogNumber
    {
        get => _previousCatalogNumber;
        set => _previousCatalogNumber = Normaliser(value);
    }

    private string? _datasetId;
    public string? DatasetId
    {
        get => _datasetId;
        set => _datasetId = Normaliser(value);
    }

    private string? _datasetName;
    public string? DatasetName
    {
        get => _datasetName;
        set => _datasetName = Normaliser(value);
    }

    private string? _relatedCatalogItem;
    public string? RelatedCatalogItem
    {
        get => _relatedCatalogItem;
        set => _relatedCatalogItem = Normaliser(value);
    }

    private string? _relationshipType;
    public string? RelationshipType
    {
        get => _relationshipType;
        set => _relationshipType = Normaliser(value);
    }

    private string? _associatedOccurrences;
    public string? AssociatedOccurrences
    {
        get => _associatedOccurrences;
        set => _associatedOccurrences = Normaliser(value);
    }

    private string? _associatedSequences;
    public string? AssociatedSequences
    {
        get => _associatedSequences;
        set => _associatedSequences = Normaliser(value);
    }

    private string? _associatedReferences;
    public string? AssociatedReferences
    {
        get => _associatedReferences;
        set => _associatedReferences = Normaliser(value);
    }

    private string? _associatedMedia;
    public string? AssociatedMedia
    {
        get => _associatedMedia;
        set => _associatedMedia = Normaliser(value);
    }

    // --- Funntype, atferd og mengde ----------------------------------------

    private string? _basisOfRecord;
    public string? BasisOfRecord
    {
        get => _basisOfRecord;
        set => _basisOfRecord = Normaliser(value);
    }

    private string? _behavior;
    public string? Behavior
    {
        get => _behavior;
        set => _behavior = Normaliser(value);
    }

    public bool Antropokor { get; set; }

    public string? EstablishmentMeans { get; set; }

    public string? OccurrenceStatus { get; set; }

    private string? _typeStatus;
    public string? TypeStatus
    {
        get => _typeStatus;
        set => _typeStatus = Normaliser(value);
    }

    private string? _status;
    public string? Status
    {
        get => _status;
        set => _status = Normaliser(value);
    }

    private string? _preparationType;
    public string? PreparationType
    {
        get => _preparationType;
        set => _preparationType = Normaliser(value);
    }

    private string? _collectingMethod;
    public string? CollectingMethod
    {
        get => _collectingMethod;
        set => _collectingMethod = Normaliser(value);
    }

    public int RelativeAboundance { get; set; }

    private string? _individualCount;
    public string? IndividualCount
    {
        get => _individualCount;
        set => _individualCount = Normaliser(value);
    }

    public string? OrganismQuantity { get; set; }

    public string? OrganismQuantityType { get; set; }

    private string? _sex;
    public string? Sex
    {
        get => _sex;
        set => _sex = Normaliser(value);
    }

    // --- Geometri og sted ---------------------------------------------------

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? GeodeticDatum { get; set; }

    public int UTMnord { get; set; }

    public int UTMost { get; set; }

    public short UTMsone { get; set; }

    private string? _mgrsNotation;
    public string? MGRSNotation
    {
        get => _mgrsNotation;
        set => _mgrsNotation = Normaliser(value);
    }

    private string? _koordinatKilde;
    public string? KoordinatKilde
    {
        get => _koordinatKilde;
        set => _koordinatKilde = Normaliser(value);
    }

    public string? CoordinateUncertaintyInMeters { get; set; }

    public string? FootprintWkt { get; set; }

    private string? _locality;
    public string? Locality
    {
        get => _locality;
        set => _locality = Normaliser(value);
    }

    public string? LocationID { get; set; }

    public string? LocationRemarks { get; set; }

    private string? _country;
    public string? Country
    {
        get => _country;
        set => _country = Normaliser(value);
    }

    private string? _county;
    public string? County
    {
        get => _county;
        set => _county = Normaliser(value);
    }

    private string? _stateProvince;
    public string? StateProvince
    {
        get => _stateProvince;
        set => _stateProvince = Normaliser(value);
    }

    private string? _continentOcean;
    public string? ContinentOcean
    {
        get => _continentOcean;
        set => _continentOcean = Normaliser(value);
    }

    public string? MinimumElevationInMeters { get; set; }

    public string? MaximumElevationInMeters { get; set; }

    private string? _elevationKilde;
    public string? ElevationKilde
    {
        get => _elevationKilde;
        set => _elevationKilde = Normaliser(value);
    }

    private string? _minimumDepthInMeters;
    public string? MinimumDepthInMeters
    {
        get => _minimumDepthInMeters;
        set => _minimumDepthInMeters = Normaliser(value);
    }

    private string? _maximumDepth;
    public string? MaximumDepth
    {
        get => _maximumDepth;
        set => _maximumDepth = Normaliser(value);
    }

    // --- Taksonomi ----------------------------------------------------------

    private string? _scientificName;
    public string? ScientificName
    {
        get => _scientificName;
        set => _scientificName = Normaliser(value);
    }

    private string? _scientificNameAuthor;
    public string? ScientificNameAuthor
    {
        get => _scientificNameAuthor;
        set => _scientificNameAuthor = Normaliser(value);
    }

    private string? _scientificNameId;
    public string? ScientificNameID
    {
        get => _scientificNameId;
        set => _scientificNameId = Normaliser(value);
    }

    public string? TaxonRank { get; set; }

    public string? NorskNavn { get; set; }

    private string? _kingdom;
    public string? Kingdom
    {
        get => _kingdom;
        set => _kingdom = Normaliser(value);
    }

    private string? _phylum;
    public string? Phylum
    {
        get => _phylum;
        set => _phylum = Normaliser(value);
    }

    private string? _class;
    public string? Class
    {
        get => _class;
        set => _class = Normaliser(value);
    }

    private string? _order;
    public string? Order
    {
        get => _order;
        set => _order = Normaliser(value);
    }

    private string? _family;
    public string? Family
    {
        get => _family;
        set => _family = Normaliser(value);
    }

    private string? _genus;
    public string? Genus
    {
        get => _genus;
        set => _genus = Normaliser(value);
    }

    private string? _species;
    public string? Species
    {
        get => _species;
        set => _species = Normaliser(value);
    }

    private string? _subspecies;
    public string? Subspecies
    {
        get => _subspecies;
        set => _subspecies = Normaliser(value);
    }

    public string? IdentificationQualifier { get; set; }

    /// <summary>
    /// Settes også av importen selv: <c>TaxonomyHelper</c> slår den på når
    /// <c>aff</c>/<c>cf</c> fjernes fra artsnavnet, og den gir <c>Tag.UnsureIdentification</c>.
    /// </summary>
    public bool IdentificationPrecision { get; set; }

    public string? IdentificationRemarks { get; set; }

    public string? IdentificationVerificationStatus { get; set; }

    private string? _identifiedBy;
    public string? IdentifiedBy
    {
        get => _identifiedBy;
        set => _identifiedBy = Normaliser(value);
    }

    private string? _identifiedById;
    public string? IdentifiedByID
    {
        get => _identifiedById;
        set => _identifiedById = Normaliser(value);
    }

    // --- Datoer -------------------------------------------------------------
    // De seks Collected/Identified-feltene er short og mangler «ikke satt»-verdi;
    // 0 betyr at feltet ikke fulgte med. Datovalideringen tolker dem.

    public short YearCollected { get; set; }

    public short MonthCollected { get; set; }

    public short DayCollected { get; set; }

    public short YearIdentified { get; set; }

    public short MonthIdentified { get; set; }

    public short DayIdentified { get; set; }

    private string? _julianDay;
    public string? JulianDay
    {
        get => _julianDay;
        set => _julianDay = Normaliser(value);
    }

    private string? _timeOfDay;
    public string? TimeOfDay
    {
        get => _timeOfDay;
        set => _timeOfDay = Normaliser(value);
    }

    public string? EventDate { get; set; }

    public string? EventTime { get; set; }

    public string? VerbatimEventDate { get; set; }

    public int? StartDayOfYear { get; set; }

    public int? EndDayOfYear { get; set; }

    public int? Year { get; set; }

    public int? Month { get; set; }

    public int? Day { get; set; }

    // --- Innsamling, hendelse og fritekst -----------------------------------

    private string? _collector;
    public string? Collector
    {
        get => _collector;
        set => _collector = Normaliser(value);
    }

    private string? _collectorNumber;
    public string? CollectorNumber
    {
        get => _collectorNumber;
        set => _collectorNumber = Normaliser(value);
    }

    private string? _recordedById;
    public string? RecordedByID
    {
        get => _recordedById;
        set => _recordedById = Normaliser(value);
    }

    public string? EventID { get; set; }

    public string? ParentEventID { get; set; }

    public string? SamplingProtocol { get; set; }

    public string? SampleSizeValue { get; set; }

    public string? SampleSizeUnit { get; set; }

    public string? SamplingEffort { get; set; }

    private string? _fieldNumber;
    public string? FieldNumber
    {
        get => _fieldNumber;
        set => _fieldNumber = Normaliser(value);
    }

    public string? FieldNotes { get; set; }

    public string? EventRemarks { get; set; }

    private string? _habitat;
    public string? Habitat
    {
        get => _habitat;
        set => _habitat = Normaliser(value);
    }

    private string? _okologi;
    public string? Okologi
    {
        get => _okologi;
        set => _okologi = Normaliser(value);
    }

    private string? _substrat;
    public string? Substrat
    {
        get => _substrat;
        set => _substrat = Normaliser(value);
    }

    /// <summary>
    /// Importen skriver også hit: flere steg legger på forklarende tekst når en verdi
    /// ikke kunne tolkes, og teksten havner til slutt i <c>ObservationDetail.Notes</c>.
    /// </summary>
    private string? _notes;
    public string? Notes
    {
        get => _notes;
        set => _notes = Normaliser(value);
    }

    public string? RightsHolder { get; set; }

    // --- Dynamiske felt og utvidelser ---------------------------------------

    /// <summary>
    /// Finnes både på <c>EventCore</c> og <c>DarwinCoreProxy</c> i Artskart 2, men er
    /// ett og samme JSON-felt.
    /// </summary>
    public string? DynamicProperties { get; set; }

    /// <summary>
    /// Darwin Core-felt harvest ikke kjente igjen, lagret som de kom. Importen leser
    /// blant annet <c>municipality</c> og <c>species</c> herfra.
    /// </summary>
    public Dictionary<string, string>? UnknownFields { get; set; }

    public CacheMultimedia[]? Multimedias { get; set; }

    public CacheMeasurementOrFact[]? MeasurementOrFacts { get; set; }

    public CacheExtendedMeasurementOrFact[]? ExtendedMeasurementOrFacts { get; set; }
}

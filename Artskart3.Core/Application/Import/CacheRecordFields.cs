using System.Globalization;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Hvilke felt på <see cref="CacheRecord"/> som kan settes utenfra, og hvordan.
///
/// EN EKSPLISITT TABELL, IKKE REFLEKSJON. Artskart 2 slår opp feltnavnet med
/// <c>GetProperty</c> og setter verdien med <c>Convert.ChangeType</c>. Det virker,
/// men det gir tre problemer: en omdøpt property blir en kjøretidsfeil i stedet for en
/// kompileringsfeil, konverteringen følger trådens kultur, og det finnes ingen liste
/// over lovlige feltnavn å vise fram i et grensesnitt. Tabellen løser alle tre.
///
/// TO BRUKERE DELER DEN: <see cref="ImportPropertyOverrides"/> fra
/// datakilderegisteret, og annotasjonene, som også setter navngitte felt på en post.
/// Da finnes koblingen mellom feltnavn og modell på ett sted.
///
/// NAVNENE ER ARTSKART 2s PROPERTYNAVN på <c>DarwinCoreProxy</c>, og oppslaget er
/// bokstavstørrelsesfølsomt — akkurat som <c>GetProperty</c>. En konfigurasjon som
/// virker i Artskart 2 virker her, og en som ikke gjør det feiler i begge.
///
/// HVA SOM IKKE ER MED: <c>Multimedias</c>, <c>MeasurementOrFacts</c>,
/// <c>ExtendedMeasurementOrFacts</c> og <c>UnknownFields</c> er samlinger. Artskart 2
/// kunne ikke sette dem fra en tekstverdi heller — <c>Convert.ChangeType</c> kaster.
/// <c>CacheRowId</c> og <c>HashCode</c> er lagt til i Artskart 3 og finnes ikke som
/// feltnavn i noen konfigurasjon.
/// </summary>
public static class CacheRecordFields
{
    private static readonly Dictionary<string, Action<CacheRecord, string?>> Tabell = new(StringComparer.Ordinal)
    {
        ["Id"] = (r, v) => r.Id = v,
        ["NodeDatabaseID"] = (r, v) => r.NodeDatabaseID = TilInt(v),
        ["IsDeleted"] = (r, v) => r.IsDeleted = TilBool(v),
        ["DateLastModified"] = (r, v) => r.DateLastModified = TilDato(v),
        ["TrackInfoDateImported"] = (r, v) => r.TrackInfoDateImported = TilDato(v),
        ["OccurrenceID"] = (r, v) => r.OccurrenceID = v,
        ["CatalogNumber"] = (r, v) => r.CatalogNumber = v,
        ["InstitutionCode"] = (r, v) => r.InstitutionCode = v,
        ["CollectionCode"] = (r, v) => r.CollectionCode = v,
        ["OwnerInstitutionCode"] = (r, v) => r.OwnerInstitutionCode = v,
        ["PreviousCatalogNumber"] = (r, v) => r.PreviousCatalogNumber = v,
        ["DatasetId"] = (r, v) => r.DatasetId = v,
        ["DatasetName"] = (r, v) => r.DatasetName = v,
        ["RelatedCatalogItem"] = (r, v) => r.RelatedCatalogItem = v,
        ["RelationshipType"] = (r, v) => r.RelationshipType = v,
        ["AssociatedOccurrences"] = (r, v) => r.AssociatedOccurrences = v,
        ["AssociatedSequences"] = (r, v) => r.AssociatedSequences = v,
        ["AssociatedReferences"] = (r, v) => r.AssociatedReferences = v,
        ["AssociatedMedia"] = (r, v) => r.AssociatedMedia = v,
        ["BasisOfRecord"] = (r, v) => r.BasisOfRecord = v,
        ["Behavior"] = (r, v) => r.Behavior = v,
        ["Antropokor"] = (r, v) => r.Antropokor = TilBool(v),
        ["EstablishmentMeans"] = (r, v) => r.EstablishmentMeans = v,
        ["OccurrenceStatus"] = (r, v) => r.OccurrenceStatus = v,
        ["TypeStatus"] = (r, v) => r.TypeStatus = v,
        ["Status"] = (r, v) => r.Status = v,
        ["PreparationType"] = (r, v) => r.PreparationType = v,
        ["CollectingMethod"] = (r, v) => r.CollectingMethod = v,
        ["RelativeAboundance"] = (r, v) => r.RelativeAboundance = TilInt(v),
        ["IndividualCount"] = (r, v) => r.IndividualCount = v,
        ["OrganismQuantity"] = (r, v) => r.OrganismQuantity = v,
        ["OrganismQuantityType"] = (r, v) => r.OrganismQuantityType = v,
        ["Sex"] = (r, v) => r.Sex = v,
        ["Latitude"] = (r, v) => r.Latitude = TilDouble(v),
        ["Longitude"] = (r, v) => r.Longitude = TilDouble(v),
        ["GeodeticDatum"] = (r, v) => r.GeodeticDatum = v,
        ["UTMnord"] = (r, v) => r.UTMnord = TilInt(v),
        ["UTMost"] = (r, v) => r.UTMost = TilInt(v),
        ["UTMsone"] = (r, v) => r.UTMsone = TilShort(v),
        ["MGRSNotation"] = (r, v) => r.MGRSNotation = v,
        ["KoordinatKilde"] = (r, v) => r.KoordinatKilde = v,
        ["CoordinateUncertaintyInMeters"] = (r, v) => r.CoordinateUncertaintyInMeters = v,
        ["FootprintWkt"] = (r, v) => r.FootprintWkt = v,
        ["Locality"] = (r, v) => r.Locality = v,
        ["LocationID"] = (r, v) => r.LocationID = v,
        ["LocationRemarks"] = (r, v) => r.LocationRemarks = v,
        ["Country"] = (r, v) => r.Country = v,
        ["County"] = (r, v) => r.County = v,
        ["StateProvince"] = (r, v) => r.StateProvince = v,
        ["ContinentOcean"] = (r, v) => r.ContinentOcean = v,
        ["MinimumElevationInMeters"] = (r, v) => r.MinimumElevationInMeters = v,
        ["MaximumElevationInMeters"] = (r, v) => r.MaximumElevationInMeters = v,
        ["ElevationKilde"] = (r, v) => r.ElevationKilde = v,
        ["MinimumDepthInMeters"] = (r, v) => r.MinimumDepthInMeters = v,
        ["MaximumDepth"] = (r, v) => r.MaximumDepth = v,
        ["ScientificName"] = (r, v) => r.ScientificName = v,
        ["ScientificNameAuthor"] = (r, v) => r.ScientificNameAuthor = v,
        ["ScientificNameID"] = (r, v) => r.ScientificNameID = v,
        ["TaxonRank"] = (r, v) => r.TaxonRank = v,
        ["NorskNavn"] = (r, v) => r.NorskNavn = v,
        ["Kingdom"] = (r, v) => r.Kingdom = v,
        ["Phylum"] = (r, v) => r.Phylum = v,
        ["Class"] = (r, v) => r.Class = v,
        ["Order"] = (r, v) => r.Order = v,
        ["Family"] = (r, v) => r.Family = v,
        ["Genus"] = (r, v) => r.Genus = v,
        ["Species"] = (r, v) => r.Species = v,
        ["Subspecies"] = (r, v) => r.Subspecies = v,
        ["IdentificationQualifier"] = (r, v) => r.IdentificationQualifier = v,
        ["IdentificationPrecision"] = (r, v) => r.IdentificationPrecision = TilBool(v),
        ["IdentificationRemarks"] = (r, v) => r.IdentificationRemarks = v,
        ["IdentificationVerificationStatus"] = (r, v) => r.IdentificationVerificationStatus = v,
        ["IdentifiedBy"] = (r, v) => r.IdentifiedBy = v,
        ["IdentifiedByID"] = (r, v) => r.IdentifiedByID = v,
        ["YearCollected"] = (r, v) => r.YearCollected = TilShort(v),
        ["MonthCollected"] = (r, v) => r.MonthCollected = TilShort(v),
        ["DayCollected"] = (r, v) => r.DayCollected = TilShort(v),
        ["YearIdentified"] = (r, v) => r.YearIdentified = TilShort(v),
        ["MonthIdentified"] = (r, v) => r.MonthIdentified = TilShort(v),
        ["DayIdentified"] = (r, v) => r.DayIdentified = TilShort(v),
        ["JulianDay"] = (r, v) => r.JulianDay = v,
        ["TimeOfDay"] = (r, v) => r.TimeOfDay = v,
        ["EventDate"] = (r, v) => r.EventDate = v,
        ["EventTime"] = (r, v) => r.EventTime = v,
        ["VerbatimEventDate"] = (r, v) => r.VerbatimEventDate = v,
        ["StartDayOfYear"] = (r, v) => r.StartDayOfYear = TilIntEllerNull(v),
        ["EndDayOfYear"] = (r, v) => r.EndDayOfYear = TilIntEllerNull(v),
        ["Year"] = (r, v) => r.Year = TilIntEllerNull(v),
        ["Month"] = (r, v) => r.Month = TilIntEllerNull(v),
        ["Day"] = (r, v) => r.Day = TilIntEllerNull(v),
        ["Collector"] = (r, v) => r.Collector = v,
        ["CollectorNumber"] = (r, v) => r.CollectorNumber = v,
        ["RecordedByID"] = (r, v) => r.RecordedByID = v,
        ["EventID"] = (r, v) => r.EventID = v,
        ["ParentEventID"] = (r, v) => r.ParentEventID = v,
        ["SamplingProtocol"] = (r, v) => r.SamplingProtocol = v,
        ["SampleSizeValue"] = (r, v) => r.SampleSizeValue = v,
        ["SampleSizeUnit"] = (r, v) => r.SampleSizeUnit = v,
        ["SamplingEffort"] = (r, v) => r.SamplingEffort = v,
        ["FieldNumber"] = (r, v) => r.FieldNumber = v,
        ["FieldNotes"] = (r, v) => r.FieldNotes = v,
        ["EventRemarks"] = (r, v) => r.EventRemarks = v,
        ["Habitat"] = (r, v) => r.Habitat = v,
        ["Okologi"] = (r, v) => r.Okologi = v,
        ["Substrat"] = (r, v) => r.Substrat = v,
        ["Notes"] = (r, v) => r.Notes = v,
        ["RightsHolder"] = (r, v) => r.RightsHolder = v,
        ["DynamicProperties"] = (r, v) => r.DynamicProperties = v,
    };

    /// <summary>Feltnavnene som kan settes. Kan vises fram i et grensesnitt.</summary>
    public static IReadOnlyCollection<string> Names => Tabell.Keys;

    /// <summary>Setteren for et feltnavn, eller <c>null</c> om navnet ikke finnes.</summary>
    public static Action<CacheRecord, string?>? Setter(string name) => Tabell.GetValueOrDefault(name);

    /// <summary>Om feltnavnet kan settes.</summary>
    public static bool CanSet(string name) => Tabell.ContainsKey(name);

    // --- Konvertering ------------------------------------------------------
    //
    // Artskart 2 bruker Convert.ChangeType uten IFormatProvider, altså trådens
    // kultur. For en overstyring skrevet som JSON i et register er det galt: samme
    // konfigurasjon ville gitt ulike tall på to servere. Her leses tall og datoer
    // alltid invariant.
    //
    // En verdi som ikke lar seg lese kaster, slik den gjør i Artskart 2 — men her
    // skjer det når overstyringene leses, før noden begynner, ikke midt i en batch.

    private static int TilInt(string? verdi) =>
        int.Parse(Påkrevd(verdi, "heltall"), CultureInfo.InvariantCulture);

    private static int? TilIntEllerNull(string? verdi) =>
        string.IsNullOrWhiteSpace(verdi) ? null : int.Parse(verdi, CultureInfo.InvariantCulture);

    private static short TilShort(string? verdi) =>
        short.Parse(Påkrevd(verdi, "heltall"), CultureInfo.InvariantCulture);

    private static double TilDouble(string? verdi) =>
        double.Parse(Påkrevd(verdi, "desimaltall"), CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>true</c>/<c>false</c> uten hensyn til bokstavstørrelse, som
    /// <c>Convert.ToBoolean</c>. «1» og «0» er ikke gyldige der, og er det ikke her.
    /// </summary>
    private static bool TilBool(string? verdi) => bool.Parse(Påkrevd(verdi, "bool"));

    private static DateTime TilDato(string? verdi) =>
        DateTime.Parse(Påkrevd(verdi, "dato"), CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static string Påkrevd(string? verdi, string type) =>
        verdi ?? throw new FormatException($"Feltet krever et {type}, men verdien er null.");
}

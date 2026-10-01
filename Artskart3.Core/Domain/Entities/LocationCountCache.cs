namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Ferdig talte observasjoner per lokasjon, for ett filter om gangen.
///
/// HVA DEN LØSER
/// Lokasjonssøket bruker rundt to sekunder på å aggregere og levere 100 000
/// lokasjoner for Oslo-utsnittet, nesten uavhengig av filter. Det er et gulv:
/// etter at områdefilteret og polygonstien er ryddet, er de tregeste kallene
/// ikke lenger trege filtre, men kostnaden ved selve aggregeringen. Målt mot en
/// ferdig tabell: 2119 → 84 ms ufiltrert, 1572 → 32 ms for én taksongruppe.
///
/// KOORDINATENE LIGGER HER MED VILJE
/// East og North er med fordi kartutsnittet er det første som snevrer inn. Uten
/// dem måtte Location joines før sorteringen, og det var nettopp det som kostet
/// 2332 ms i den opprinnelige analysen. Latitude og Longitude er derimot IKKE
/// med: de trengs bare for de 100 000 radene som faktisk returneres, og joinen
/// for dem er målt til 186 ms. Å bære dem på alle 77 millioner radene ville
/// kostet 1,2 GB ekstra.
///
/// BARE ETT NIVÅ
/// Bufferen lagrer marginaler — «antall observasjoner på lokasjon L i bøtte B av
/// dimensjon D». To filtre krever samfordelingen, og den kan ikke utledes: en
/// observasjon som treffer begge ligger i begge bøttene, så summering overteller
/// og minimum er bare en øvre grense. Et nivå 2 med parbøtter er mulig — målt
/// 5,3 til 21,9 mill. rader per par, rundt 16 GB for alle 66 — men to-filter-
/// spørringene er ikke de trege (389 ms i snitt), så nivå 1 treffer der det gjør
/// vondt.
/// </summary>
public class LocationCountCacheLevel1
{
    /// <summary>
    /// Dimensjonen fra AreaCountCacheDimensions. 0 er reservert for «uten
    /// filter», som ikke er en dimensjon der men er det vanligste kallet her.
    /// </summary>
    public byte DimensionId { get; set; }

    /// <summary>Bøtta innenfor dimensjonen. For DimensionId 0 alltid 0.</summary>
    public int BucketId { get; set; }

    public int LocationId { get; set; }

    /// <summary>UTM 33N. Kartutsnittet filtreres på disse.</summary>
    public int East { get; set; }

    public int North { get; set; }

    /// <summary>Distinkte observasjoner, ikke indeksrader.</summary>
    public int ObservationCount { get; set; }
}

/// <summary>
/// Om lokasjonsbufferen er bygget og gyldig. Én rad, <see cref="PointCacheId"/>.
///
/// Egen tabell og ikke en rad i AreaCountCacheState: de to bufferne bygges
/// uavhengig, og en halvbygget lokasjonsbuffer skal ikke kunne se ferdig ut
/// fordi områdebufferen er det.
///
/// Id-en er beholdt som eksplisitt konstant selv om det bare finnes én buffer:
/// det fantes en rad 2 for polygonlokasjoner, og den ble fjernet da
/// polygonsøket gikk over til PolygonLocationStore. En ny buffer skal ikke
/// arve nummeret ved et uhell.
/// </summary>
public class LocationCountCacheState
{
    /// <summary>Tilstandsraden for <see cref="LocationCountCacheLevel1"/>.</summary>
    public const byte PointCacheId = 1;

    public byte Id { get; set; }

    /// <summary>
    /// AreaCountCacheDimensions.SchemaVersion da bufferen ble bygget. Endres
    /// dimensjonsregisteret, er bøtte-id-ene i tabellen ikke lenger de samme som
    /// oppslaget ville beregnet, og bufferen må bygges på nytt.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>'Building' eller 'Ready'. Bare 'Ready' brukes.</summary>
    public string Status { get; set; } = null!;

    public DateTime? BuiltAt { get; set; }

    public long? SourceRows { get; set; }

    public int? DurationSeconds { get; set; }
}

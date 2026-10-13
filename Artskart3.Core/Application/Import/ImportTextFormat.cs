using System.Globalization;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Kulturen importen formaterer tekst med når teksten skal bli stående i basen.
///
/// HVORFOR DEN ER PINNET: Artskart 2 formaterer uten å oppgi kultur — <c>$"{date}"</c>,
/// <c>ToShortDateString()</c> og tilsvarende — og arver derfor kulturen prosessen
/// tilfeldigvis startet med. Verdiene havner i <c>ObservationDetail.Notes</c> og
/// <c>ObservationError.ErrorContext</c>, altså i brukersynlig tekst som ligger i 61
/// millioner rader. En port som leser vertsmaskinens kultur ville gitt forskjellig
/// tekst på en utviklermaskin og en tjener — og det er nøyaktig den feilen som har
/// gjort at formatet må måles i stedet for antas.
///
/// HVORFOR nb-NO: det er formatet som faktisk ligger i basen. Målt i
/// <c>Artskart3IndexProdLikeTestMigrations</c>:
///
/// <list type="bullet">
/// <item><c>Notes</c>: «Veldig gammel DateLastModified: 02.01.0001 00:00:00»</item>
/// <item><c>ObservationError.ErrorContext</c>: «DateTimeCollected: 12.05.1666»</item>
/// </list>
///
/// nb-NO gjengir begge tegn for tegn. Produksjonstjeneren har altså kjørt med norsk
/// kultur, og nye rader skal se ut som de 61 millionene som alt står der.
///
/// AVVIK MOT GULLSETTENE, OG DET ER TILSIKTET: gullsettene er tatt ut ved å kjøre
/// Artskart 2 på en maskin med en-US, som gir «5/1/1650 12:00:00 AM». Det er maskinens
/// locale som avviker, ikke Artskart 2s oppførsel, så paritetsriggen må føre dette som
/// et bevisst avvik på <c>Notes</c> og <c>ErrorContext</c> — eller normalisere feltet
/// før sammenligning. Å velge en-US her for å få riggen grønn ville gitt to formater i
/// samme kolonne i produksjon.
///
/// ALLE STEG SOM SKRIVER TEKST TIL BASEN SKAL BRUKE DENNE. Det gjelder også tall:
/// <c>ImportGeometry</c> i Artskart 2 skriver koordinater inn i meldinger på samme
/// måte, og desimalskilletegnet følger kulturen.
/// </summary>
public static class ImportTextFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("nb-NO");

    /// <summary>Dato og klokkeslett, slik <c>$"{date}"</c> gir det: <c>01.05.1650 00:00:00</c>.</summary>
    public static string DateAndTime(DateTime? value) =>
        value?.ToString(Culture) ?? string.Empty;

    /// <summary>Kort dato, slik <c>ToShortDateString()</c> gir det: <c>01.05.1650</c>.</summary>
    public static string ShortDate(DateTime? value) =>
        value?.ToString("d", Culture) ?? string.Empty;
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Helpers;

/// <summary>
/// Port av Artskart 2s <c>ImportTaskHelper</c> og de av <c>StringHelper</c>-metodene
/// importen bruker. Ren tolkning av enkeltverdier, ingen oppslag.
/// </summary>
public static class ImportValueHelper
{
    /// <summary>
    /// Leser et JSON-objekt som streng-par, med den samme typekonverteringen
    /// <c>JavaScriptSerializer</c> gjorde: tall og bool blir tekst, mens nøstede
    /// objekter og arrayer ikke kan bli det — og da kastes det.
    ///
    /// Bool skrives som <c>True</c>/<c>False</c>, slik .NETs
    /// <c>Convert.ToString(bool)</c> gjør det, ikke som JSON-ens <c>true</c>.
    ///
    /// Brukes både på <c>dynamicProperties</c> fra kilden og på
    /// <c>ImportPropertyOverrides</c> fra datakilderegisteret — begge er JSON skrevet
    /// mot <c>JavaScriptSerializer</c>.
    /// </summary>
    /// <param name="json">JSON-objektet.</param>
    /// <param name="kilde">Feltnavnet, som brukes i feilmeldingen.</param>
    public static Dictionary<string, string?> ReadJsonAsTextPairs(string json, string kilde)
    {
        var lest = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? throw new JsonException($"{kilde} deserialiserte til null.");

        var resultat = new Dictionary<string, string?>();

        foreach (var par in lest)
        {
            resultat.Add(par.Key, par.Value.ValueKind switch
            {
                JsonValueKind.String => par.Value.GetString(),
                JsonValueKind.Number => par.Value.GetRawText(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Null => null,
                _ => throw new JsonException(
                    $"{kilde}-nøkkelen '{par.Key}' er {par.Value.ValueKind} og kan ikke bli tekst.")
            });
        }

        return resultat;
    }

    /// <summary>
    /// «2 meter», «ca 50», «12.7» → tall. Brukes på
    /// <c>CoordinateUncertaintyInMeters</c> og høyde-/dybdefeltene.
    ///
    /// Rekkefølgen er Artskart 2s, og den er ikke tilfeldig:
    /// 1. <c>meters|meter|m|ca</c> fjernes hvor som helst i strengen, uansett
    ///    bokstavstørrelse. Alternativet <c>m</c> treffer enhver m, så «2 metres»
    ///    blir «2 etres» og ender som ugyldig. Det er dagens oppførsel.
    /// 2. Desimalskilletegn normaliseres.
    /// 3. Finnes et skilletegn, rundes tallet ned til int — men en verdi i
    ///    intervallet (0, 1] rundes OPP til 1, slik at en presisjon på en halv meter
    ///    ikke blir «ingen presisjon».
    /// 4. Bare tall strengt større enn 0 godtas. 0 og negative tall gir merknad.
    ///
    /// INVARIANT KULTUR, IKKE TRÅDENS. Artskart 2 leser skilletegnet fra
    /// <c>Thread.CurrentThread.CurrentCulture</c> og normaliserer til det, for så å
    /// parse med samme kultur. Utfallet er derfor det samme enten kulturen er nb-NO
    /// eller invariant: ett skilletegn tolkes likt, og verdier med både punktum og
    /// komma feiler i begge. Her står det fast, så en importkjøring ikke avhenger av
    /// hvilken kultur tjenesten tilfeldigvis startet med.
    /// </summary>
    public static int? ExtractIntegerIfPossible(string? input, string propertyName, IList<ImportIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var temp = Regex.Replace(input, "(meters|meter|m|ca)", string.Empty, RegexOptions.IgnoreCase).Trim();
        temp = temp.Replace(",", ".");

        if (temp.Contains('.'))
        {
            if (double.TryParse(temp, NumberStyles.Float, CultureInfo.InvariantCulture, out var tall))
            {
                temp = ((int)tall).ToString(CultureInfo.InvariantCulture);

                if (tall < 1 && tall > 0.0000001)
                {
                    temp = "1";
                }
            }
        }

        if (int.TryParse(temp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resultat) && resultat > 0)
        {
            return resultat;
        }

        issues.Add(new ImportIssue(propertyName, input, ImportIssueRemark.NoValidInteger));

        return null;
    }

    /// <summary>
    /// Mer enn seks desimaler avrundes til seks, med «banker's rounding» slik
    /// Artskart 2 gjør det. Seks desimaler er omtrent ti centimeter ved våre
    /// breddegrader.
    ///
    /// Avviker resultatet fra kildeverdien, lagrer <c>ImportGeometry</c> originalen som
    /// <c>_original_Latitude</c> / <c>_original_Longitude</c>.
    /// </summary>
    public static double SetDigitsForLatLon(double value) =>
        TellDesimaler(value) > 6 ? Math.Round(value, 6, MidpointRounding.ToEven) : value;

    /// <summary>
    /// Reparasjoner på <c>DynamicProperties</c> før strengen tolkes som JSON. Port av
    /// <c>JsonSanityFixes</c>: noen kilder leverer et objekt der den første
    /// anførselstegnet mangler, og enkelte leverer uescapede bakstreker.
    /// </summary>
    public static string? JsonSanityFixes(string? dynamicProperties)
    {
        if (string.IsNullOrWhiteSpace(dynamicProperties))
        {
            return dynamicProperties;
        }

        var resultat = dynamicProperties;

        if (dynamicProperties.StartsWith('{')
            && !dynamicProperties.StartsWith("{\"")
            && dynamicProperties.EndsWith('"'))
        {
            resultat = "{\"" + dynamicProperties.Substring(1, dynamicProperties.Length - 2);
        }

        if (resultat.Contains('\\'))
        {
            resultat = resultat.Replace("\\", "\\\\");
        }

        return resultat;
    }

    /// <summary>
    /// De første <paramref name="length"/> tegnene. Port av <c>StringHelper.Left</c>,
    /// som feltmatrisen viser til overalt der en verdi må passe i en kolonne.
    /// <c>null</c> inn gir <c>null</c> ut — ikke tom streng; forskjellen avgjør om
    /// nedstrøms <c>IsNullOrEmpty</c>-sjekker slår til.
    /// </summary>
    public static string? Left(string? value, int length)
    {
        if (value is null) return null;

        return length > value.Length ? value : value[..length];
    }

    /// <summary>
    /// Som <see cref="Left"/>, men setter <c>...</c> til slutt når det faktisk ble
    /// kuttet noe og grensen er over ti tegn. Port av <c>StringHelper.DotLeft</c>,
    /// som Artskart 2 bruker på <c>ErrorContext</c>.
    /// </summary>
    public static string? DotLeft(string? value, int length)
    {
        if (value is null) return null;

        // Merk: betingelsen er «strengt større», slik Artskart 2 har den. En streng som
        // er nøyaktig like lang som grensen får derfor punktene — samme lengde, men de
        // tre siste tegnene byttes. Beholdt fordi ErrorContext-verdier i basen er
        // skrevet med den regelen.
        if (length > value.Length) return value;

        var medTrePunkter = length > 10 && value.Length > 10;

        return medTrePunkter ? value[..(length - 3)] + "..." : value[..length];
    }

    /// <summary>
    /// Som <see cref="Left"/>, men fjerner alle mellomrom først. Port av
    /// <c>StringHelper.NonSpaceLeft</c>.
    /// </summary>
    public static string? NonSpaceLeft(string? value, int length)
    {
        if (value is null) return null;

        var uten = value.Replace(" ", string.Empty);

        return length > uten.Length ? uten : uten[..length];
    }

    /// <summary>
    /// Antall desimaler i tallets tekstform.
    ///
    /// MERK: <c>double.ToString()</c> ga 15 signifikante siffer i .NET Framework og gir
    /// korteste tilbakeleselige form i .NET 10. For koordinater lest fra JSON er de to
    /// like, fordi verdien allerede er korteste form. For en verdi som har vært gjennom
    /// flyttallsaritmetikk kan .NET 10 se flere desimaler enn Artskart 2 gjorde — og da
    /// runder vi, der Artskart 2 lot verdien stå. Utfallet er en verdi med færre
    /// desimaler, aldri en annen posisjon.
    /// </summary>
    private static int TellDesimaler(double value)
    {
        var tekst = value.ToString(CultureInfo.InvariantCulture);
        var deler = tekst.Split('.');

        return deler.Length == 1 ? 0 : deler[1].Length;
    }
}

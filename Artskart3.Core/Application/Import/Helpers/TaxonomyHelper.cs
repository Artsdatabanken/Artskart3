using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Helpers;

/// <summary>
/// Port av Artskart 2s <c>TaxonomyHelper</c>: utleder artsnavn og forfatterstreng fra
/// en cache-post, og normaliserer navnet til formen taksonomioppslaget bruker.
///
/// DETTE ER TOLKNING, IKKE OPPSLAG. Ingenting her slår opp i <c>Taxon</c>. Treffer
/// ikke det normaliserte navnet noe takson, avvises posten med
/// <c>NoTaxonomicMatch</c> — og da er det disse metodene som bestemte hva det ble søkt
/// på.
/// </summary>
public static class TaxonomyHelper
{
    /// <summary>Hakeparenteser, anførselstegn og runde parenteser med innhold.</summary>
    public const string ParentesPattern = "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))";

    private static readonly string[] UsikkerInni = [" aff ", " aff. ", " cf ", " cf. "];
    private static readonly string[] UsikkerStart = ["aff ", "aff. ", "cf ", "cf. "];

    /// <summary>
    /// Verdier som ikke er navn, men plassholdere for «ubestemt». Lista er Artskart 2s
    /// (<c>Nothings</c> i <c>ImportTaxonomy</c>, og den samme i
    /// <c>TaxonSearchService.SciName</c>).
    /// </summary>
    private static readonly string[] Plassholdere = ["sp", "indet", "var", "spp", "ssp", "subsp", "agg", "×"];

    /// <summary>
    /// Gir <c>null</c> hvis verdien bare er en rangforkortelse eller et
    /// «ubestemt»-ord — ellers verdien uendret. Port av Artskart 2s <c>StripIt</c>.
    ///
    /// Brukes to steder: når rangen skal utledes av hvilke rangfelt som er utfylt, og
    /// når et navn i taksonomien skal legges i navneoppslaget.
    /// </summary>
    public static string? StripRankPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var nøkkel = value.ToLowerInvariant().Trim().Replace(".", string.Empty);

        return Plassholdere.Contains(nøkkel) ? null : value;
    }

    /// <summary>
    /// Forfatterstrengen, lowercase.
    ///
    /// Mangler <c>ScientificNameAuthor</c>, GJETTES den: første ledd etter posisjon 0 i
    /// artsnavnet som er mer enn ett tegn og begynner med stor bokstav, antas å være
    /// starten på forfatterstrengen, og alt derfra og ut blir forfatter. «Carex
    /// Linnaeus 1753» gir «linnaeus 1753». Gjetningen er original oppførsel, og den
    /// bommer på navn der andre ledd er et stort slektsnavn — men den avgjør bare
    /// hvilken forfatterstreng oppslaget sammenligner med, ikke selve navnet.
    /// </summary>
    public static string ExtractAuthorString(CacheRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var authorString = !string.IsNullOrEmpty(record.ScientificNameAuthor)
            ? record.ScientificNameAuthor
            : string.Empty;

        if (string.IsNullOrWhiteSpace(authorString) && !string.IsNullOrWhiteSpace(record.ScientificName))
        {
            var ledd = record.ScientificName.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            for (var i = 1; i < ledd.Length; i++)
            {
                if (ledd[i].Length > 1 && char.IsUpper(ledd[i][0]))
                {
                    authorString = string.Join(" ", ledd.Skip(i));
                    break;
                }
            }
        }

        return authorString.ToLowerInvariant();
    }

    /// <summary>
    /// Artsnavnet slik taksonomioppslaget skal se det.
    ///
    /// SETTER <c>IdentificationPrecision</c> PÅ POSTEN. Inneholder navnet <c>aff</c>
    /// eller <c>cf</c>, fjernes leddet og flagget settes — det er dette som senere blir
    /// <c>Tag.UnsureIdentification</c>. Derfor tar metoden posten og ikke bare en
    /// streng.
    ///
    /// <paramref name="skipScientificNameField"/> brukes i Artskart 2s andre forsøk:
    /// treffer ikke navnet fra <c>ScientificName</c>, bygges et nytt navn fra
    /// rangfeltene i stedet.
    ///
    /// EN SKJEVHET SOM ER BEHOLDT: betingelsen tester om navnet inneholder
    /// <c>record.ScientificNameAuthor</c> (som den står), mens det er
    /// <paramref name="authorString"/> som fjernes. Kalles metoden med en lowercaset
    /// forfatterstreng mens navnet har stor forbokstav, slår <c>Replace</c> ikke til.
    /// Artskart 2 kaller den med feltet selv på første forsøk, så i praksis treffer den
    /// — men asymmetrien er ekte og skal ikke «ryddes» uten at utslaget er målt.
    /// </summary>
    public static string ExtractScientificName(
        CacheRecord record,
        string? authorString,
        bool skipScientificNameField = false)
    {
        ArgumentNullException.ThrowIfNull(record);

        var scientificName = !string.IsNullOrEmpty(record.ScientificName) && !skipScientificNameField
            ? record.ScientificName.Trim()
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(authorString)
            && !string.IsNullOrWhiteSpace(scientificName)
            && record.ScientificNameAuthor is not null
            && scientificName.Contains(record.ScientificNameAuthor))
        {
            scientificName = scientificName.Replace(authorString, string.Empty).Trim();
        }

        if (string.IsNullOrEmpty(scientificName) || skipScientificNameField)
        {
            scientificName = ConstructScientificNameFromOtherFields(record, scientificName);
        }

        // BOLD-navn er strekkodeklustre, ikke artsnavn. Kilden har ofte det virkelige
        // navnet i det ukjente feltet «species».
        if (scientificName.StartsWith("BOLD:", StringComparison.Ordinal)
            && record.UnknownFields is not null
            && record.UnknownFields.TryGetValue("species", out var artFraUkjenteFelt))
        {
            scientificName = artFraUkjenteFelt;
        }

        var resultat = CleanScientificName(scientificName);

        if (string.IsNullOrWhiteSpace(resultat))
        {
            return resultat;
        }

        foreach (var ledd in UsikkerInni)
        {
            if (!resultat.Contains(ledd, StringComparison.Ordinal)) continue;

            record.IdentificationPrecision = true;
            resultat = resultat.Replace(ledd, " ");
        }

        foreach (var ledd in UsikkerStart)
        {
            if (!resultat.StartsWith(ledd, StringComparison.Ordinal)) continue;

            record.IdentificationPrecision = true;
            resultat = resultat[ledd.Length..];
        }

        return resultat;
    }

    /// <summary>
    /// Bygger et navn fra rangfeltene når <c>ScientificName</c> mangler, i rekkefølgen
    /// Artskart 2 bruker: slekt+art+underart → slekt+art → slekt → familie → orden →
    /// klasse → rekke → rike. Finner den ingenting, kommer
    /// <paramref name="scientificName"/> uendret tilbake.
    /// </summary>
    public static string ConstructScientificNameFromOtherFields(CacheRecord record, string scientificName)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!string.IsNullOrWhiteSpace(record.Subspecies)
            && !string.IsNullOrWhiteSpace(record.Species)
            && !string.IsNullOrWhiteSpace(record.Genus))
        {
            return record.Genus.Trim() + " " + record.Species.Trim() + " " + record.Subspecies.Trim();
        }

        if (!string.IsNullOrWhiteSpace(record.Species) && !string.IsNullOrWhiteSpace(record.Genus))
        {
            return record.Genus.Trim() + " " + record.Species.Trim();
        }

        if (!string.IsNullOrWhiteSpace(record.Genus)) return record.Genus.Trim();
        if (!string.IsNullOrWhiteSpace(record.Family)) return record.Family.Trim();
        if (!string.IsNullOrWhiteSpace(record.Order)) return record.Order.Trim();
        if (!string.IsNullOrWhiteSpace(record.Class)) return record.Class.Trim();
        if (!string.IsNullOrWhiteSpace(record.Phylum)) return record.Phylum.Trim();
        if (!string.IsNullOrWhiteSpace(record.Kingdom)) return record.Kingdom.Trim();

        return scientificName;
    }

    /// <summary>
    /// Normaliserer et artsnavn til formen taksonomien slår opp på.
    ///
    /// REKKEFØLGEN ER EN DEL AV OPPFØRSELEN. Erstatningene kjøres etter hverandre, og
    /// et bytte kan skape treff for det neste: <c>.</c> blir mellomrom FØRST, så
    /// « var. » er blitt « var » når rangforkortelsene fjernes. Flytter man en linje,
    /// endrer man hvilke navn som treffer.
    ///
    /// Merk at <c> agg </c> ikke fjernes, men normaliseres til <c> agg. </c> — det er
    /// formen den norske artsnavnebasen bruker. Samme grunn til at <c>-group</c> blir
    /// <c>-gruppen</c>.
    /// </summary>
    public static string CleanScientificName(string scientificName)
    {
        ArgumentNullException.ThrowIfNull(scientificName);

        var navn = scientificName.ToLowerInvariant()
            .Replace(".", " ")
            .Replace(" indet", " ")
            .Replace(" var ", " ")
            .Replace(" spp ", " ")
            .Replace(" ssp ", " ")
            .Replace(" sp ", " ")
            .Replace(" subsp ", " ")
            .Replace(" agg ", " agg. ")
            .Replace(" f ", " ")
            .Replace(" form ", " ")
            .Replace("  ", " ")
            .Replace(" × ", " ×")
            .Replace("-group", "-gruppen")
            .Replace(" s s", " ")
            .Replace(" s l", " ")
            .Replace(" s str", " ")
            .Replace(" s lat", " ")
            .Replace(" sensu lato", " ")
            .Replace(" sensu stricto", " ")
            .Trim();

        return Regex.Replace(navn, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Fjerner parentes- og anførselsinnhold fra et navn. Brukes der
    /// forfatterstrengen står i parentes bak navnet.
    /// </summary>
    public static string RemoveScientificNameParanteseContent(string scientificName)
    {
        ArgumentNullException.ThrowIfNull(scientificName);

        return Regex.Replace(scientificName, ParentesPattern, " ")
            .Replace("  ", " ")
            .Replace("  ", " ")
            .Trim();
    }
}

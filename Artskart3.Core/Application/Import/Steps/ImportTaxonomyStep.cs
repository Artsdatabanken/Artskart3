using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Bestemmer hvilket takson posten gjelder. Port av Artskart 2s <c>ImportTaxonomy</c>.
///
/// AVVISER MER ENN NOE ANNET STEG. Treffer ikke navnet et takson, er posten ubrukelig:
/// <c>TaxonId</c> er en påkrevd fremmednøkkel, og hele taksonfilteret bygger på den.
/// Node 211 avviser sju av ti poster, og det er i hovedsak her det skjer.
///
/// TRE FORSØK PÅ Å FINNE NAVNET, i denne rekkefølgen:
///
/// <list type="number">
/// <item>Navnet fra <c>scientificName</c>, normalisert av
/// <c>TaxonomyHelper.ExtractScientificName</c></item>
/// <item>Navnet bygget fra rangfeltene i stedet — brukes når forfatteren ligger inne i
/// <c>scientificName</c>, eller når navnet inneholder « x » (hybrid)</item>
/// <item>Siste utvei: rangfeltene som navn, og det som blir igjen av
/// <c>scientificName</c> etter at navnet er trukket fra, som forfatterstreng</item>
/// </list>
///
/// RANGEN UTLEDES I ÅTTE TRINN når kilden ikke oppgir den, og rekkefølgen har en kjent
/// skjevhet: <c>order</c> sjekkes etter <c>phylum</c>. En post med både rekke og orden
/// utfylt får derfor <c>phylum</c>. Bevart — rangen brukes bare til å skille flertydige
/// navnetreff, så utslaget er begrenset, men det er ikke null.
///
/// SKRIVER TILBAKE PÅ POSTEN. Treffer navnet, overskrives <c>record.ScientificName</c>
/// og <c>record.ScientificNameAuthor</c> med taksonets gyldige verdier, og kildens
/// verdier lagres som <c>_original_</c>. Det er derfor annotasjoner som endrer artsnavn
/// må utløse en ny kjøring av dette steget (fase 10).
///
/// BUFRER PER OPPSLAG, per nodekjøring. Node 1010 har 37 millioner poster og noen tusen
/// distinkte navn; uten bufferen gjøres samme oppslag millioner av ganger. Nøkkelen er
/// alt de tre forsøkene leser — se <see cref="Oppslagsnøkkel"/>. Artskart 2 nøkler bare
/// på navnet, og det er en feil: der arver poster med én forfatter taksontreffet til en
/// post med en annen. Se 5d i <c>MuligeArtskart2BugsViBørAvklare.md</c>.
/// </summary>
public class ImportTaxonomyStep : IImportStep
{
    private static readonly Regex HtmlTagg = new("<[^>]*>", RegexOptions.Compiled);

    private readonly ITaxonNameResolver _resolver;
    private readonly Dictionary<Oppslagsnøkkel, TaxonMatch?> _buffer = [];

    public ImportTaxonomyStep(ITaxonNameResolver resolver)
    {
        _resolver = resolver;
    }

    public ImportStepType StepType => ImportStepType.Pre;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        // Merk at forfatterstrengen hentes med record.ScientificNameAuthor og ikke med
        // den gjettede — se asymmetrien dokumentert i TaxonomyHelper.
        var recordName = TaxonomyHelper.ExtractScientificName(record, record.ScientificNameAuthor);
        var authorString = TaxonomyHelper.ExtractAuthorString(record);

        if (string.IsNullOrWhiteSpace(recordName))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.NoTaxonomicMatch,
                RecordNotificationCode.MissingScientificName,
                Feilkontekst(record));
        }

        var treff = SlåOpp(record, recordName, authorString, out var diagnostikk);

        if (treff is null)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.NoTaxonomicMatch,
                RecordNotificationCode.NotAbleToMatchTaxonomy,
                recordName + (string.IsNullOrWhiteSpace(diagnostikk) ? string.Empty : " - " + diagnostikk));
        }

        return SkrivTakson(record, observation, context, treff);
    }

    /// <summary>
    /// De tre forsøkene, bufret på alt de leser. Bufferen holder også bomskuddene: et
    /// oppslag som ikke traff, treffer ikke neste gang heller.
    ///
    /// Som i Artskart 2 blir diagnosemeldingen tom på et bufret treff, så bare den
    /// første avviste posten får årsaken med i feilteksten.
    /// </summary>
    private TaxonMatch? SlåOpp(
        CacheRecord record,
        string recordName,
        string authorString,
        out string diagnostikk)
    {
        diagnostikk = string.Empty;

        // Oppslaget forutsetter små bokstaver. Kommer det noe annet hit, er det en feil
        // i normaliseringen og ikke i dataene — da skal det merkes, ikke tolkes videre.
        if (recordName.Any(char.IsUpper) || authorString.Any(char.IsUpper))
        {
            throw new InvalidOperationException(
                $"Taksonoppslag krever små bokstaver - '{recordName}' / '{authorString}'");
        }

        var rang = UtledRang(record, authorString);
        var nøkkel = Oppslagsnøkkel.Fra(record, recordName, authorString, rang);

        if (_buffer.TryGetValue(nøkkel, out var bufret)) return bufret;

        var treff = _resolver.Resolve(recordName, rang, authorString);

        if (treff is null) treff = ForsøkMedNavnFraRangfeltene(record, recordName, authorString, rang);
        if (treff is null) treff = ForsøkMedForfatterTrukketFraNavnet(record, recordName, rang);

        diagnostikk = treff?.Diagnostics ?? string.Empty;

        _buffer[nøkkel] = treff;

        return treff;
    }

    /// <summary>
    /// Alt de tre forsøkene leser av posten, og ingenting mer.
    ///
    /// HVORFOR SÅ MANGE FELT. Bufferen er en memoisering, så nøkkelen må være hele
    /// inndatamengden til det som memoiseres. Navn, forfatter og rang går rett inn i
    /// første oppslag. Rangfeltene bygger navnet i forsøk to og tre, og
    /// <c>ScientificName</c> er strengen forsøk tre trekker forfatteren ut av — to
    /// poster kan ha samme normaliserte navn og ulik råstreng.
    ///
    /// Mangler ett av dem i nøkkelen, arver en post svaret til en annen post som bare
    /// tilfeldigvis ble lest først. Det er feilen i Artskart 2, og den gjorde utfallet
    /// avhengig av lesesorteringen — se 5d i <c>MuligeArtskart2BugsViBørAvklare.md</c>.
    /// </summary>
    private readonly record struct Oppslagsnøkkel(
        string Navn,
        string Forfatter,
        string? Rang,
        string? ScientificName,
        string? Genus,
        string? Species,
        string? Subspecies,
        string? Family,
        string? Order,
        string? Class,
        string? Phylum,
        string? Kingdom)
    {
        public static Oppslagsnøkkel Fra(
            CacheRecord record,
            string recordName,
            string authorString,
            string? rang) =>
            new(
                recordName,
                authorString,
                rang,
                record.ScientificName,
                record.Genus,
                record.Species,
                record.Subspecies,
                record.Family,
                record.Order,
                record.Class,
                record.Phylum,
                record.Kingdom);
    }

    /// <summary>
    /// Andre forsøk: bygg navnet fra rangfeltene i stedet for <c>scientificName</c>.
    ///
    /// Betingelsen er Artskart 2s, og den er ikke åpenbar: det gjøres bare hvis
    /// forfatterstrengen IKKE finnes i navnet og navnet er kortere enn det rangfeltene
    /// gir — altså når <c>scientificName</c> ser avkortet ut — eller hvis navnet
    /// inneholder « x », som er en hybridskrivemåte rangfeltene håndterer bedre.
    /// </summary>
    private TaxonMatch? ForsøkMedNavnFraRangfeltene(
        CacheRecord record,
        string recordName,
        string authorString,
        string? rang)
    {
        var fraRangfeltene = TaxonomyHelper.ExtractScientificName(record, authorString, skipScientificNameField: true);

        if (string.IsNullOrWhiteSpace(fraRangfeltene)) return null;

        var navnetSerAvkortetUt = !recordName.Contains(authorString, StringComparison.OrdinalIgnoreCase)
                                  && recordName.Length < fraRangfeltene.Length;

        var erHybrid = recordName.Contains(" x ", StringComparison.OrdinalIgnoreCase);

        if (!navnetSerAvkortetUt && !erHybrid) return null;

        return _resolver.Resolve(fraRangfeltene, rang, authorString);
    }

    /// <summary>
    /// Tredje forsøk: anta at <c>scientificName</c> er navn pluss forfatter, der
    /// forfatteren ikke står i sitt eget felt. Navnet bygges fra rangfeltene, og det som
    /// blir igjen av <c>scientificName</c> etter at navnet er trukket fra, brukes som
    /// forfatterstreng.
    /// </summary>
    private TaxonMatch? ForsøkMedForfatterTrukketFraNavnet(CacheRecord record, string recordName, string? rang)
    {
        var navn = TaxonomyHelper.ConstructScientificNameFromOtherFields(record, recordName).ToLowerInvariant();

        var heleNavnet = (record.ScientificName ?? string.Empty).Trim().ToLowerInvariant();
        var forfatter = heleNavnet.Replace(navn, string.Empty).Trim();

        // Ble ingenting trukket fra, er dette samme forsøk som forrige. Ble alt trukket
        // fra, er det ingen forfatter å slå opp på.
        if (forfatter.Length == 0 || forfatter.Length == heleNavnet.Length) return null;

        return _resolver.Resolve(navn, rang, forfatter);
    }

    /// <summary>
    /// Fyller observasjonen fra treffet og skriver taksonets navn tilbake på posten.
    /// </summary>
    private static ImportRecordResult SkrivTakson(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context,
        TaxonMatch treff)
    {
        var notificationCode = RecordNotificationCode.Ok;

        observation.TaxonId = treff.Taxon.Id;
        observation.MatchedScientificNameId = treff.MatchedScientificNameId;

        // Hele hierarkiet blir rader i ObservationTaxons — det er det taksonfilteret
        // utvider seg over.
        observation.TaxonIds.Clear();
        observation.TaxonIds.AddRange(treff.Taxon.TaxonIdHierarchy);

        // Artskart 2 sjekker >= 0, og TaxonGroupId er en int som i praksis alltid er
        // det. Varselet NoTaxonGroupHit er derfor så godt som dødt i dagens data —
        // bevart fordi koden er det eneste stedet det kan komme fra.
        if (treff.Taxon.TaxonGroupId >= 0)
        {
            observation.TaxonGroupId = treff.Taxon.TaxonGroupId;
        }
        else
        {
            notificationCode = RecordNotificationCode.NoTaxonGroupHit;
        }

        context.TaxonProperties = treff.Taxon.Properties.ToList();
        context.AllScientificNameIds = treff.Taxon.ScientificNameIds;

        // Taksonets gyldige navn overskriver kildens, uten HTML-formatering. Kildens
        // verdi lagres først — og bare hvis den ikke alt er lagret, siden steget kan
        // kjøres om igjen etter en annotasjon.
        var gyldigNavn = HtmlTagg.Replace(treff.Taxon.ValidScientificName ?? string.Empty, string.Empty);

        if (gyldigNavn != record.ScientificName)
        {
            if (!context.HasOriginalValue("ScientificName"))
            {
                context.DynamicProperties[ImportContext.OriginalValuePrefix + "ScientificName"] =
                    record.ScientificName ?? string.Empty;
            }

            record.ScientificName = gyldigNavn;
        }

        if (treff.Taxon.ValidScientificNameAuthorship != record.ScientificNameAuthor)
        {
            if (!context.HasOriginalValue("ScientificNameAuthor"))
            {
                context.DynamicProperties[ImportContext.OriginalValuePrefix + "ScientificNameAuthor"] =
                    record.ScientificNameAuthor ?? string.Empty;
            }

            record.ScientificNameAuthor = treff.Taxon.ValidScientificNameAuthorship;
        }

        return ImportRecordResult.Ok(notificationCode);
    }

    /// <summary>
    /// Rangen kilden oppgir, eller den som kan utledes. Port av
    /// <c>TryGetRecordTaxonRank</c>.
    ///
    /// Rangen brukes bare til å skille flertydige navnetreff, så en gjetning som bommer
    /// gir i verste fall et uavgjort oppslag — ikke et galt takson.
    /// </summary>
    private static string? UtledRang(CacheRecord record, string authorString)
    {
        if (!string.IsNullOrWhiteSpace(record.TaxonRank))
        {
            var oppgitt = record.TaxonRank.ToLowerInvariant().Replace(".", string.Empty).Trim();

            switch (oppgitt)
            {
                case "var": return "variety";
                case "spp":
                case "ssp":
                case "sp":
                case "subsp": return "subspecies";
                case "f":
                case "form": return "form";
            }

            // Merk: verdien returneres som kilden skrev den, ikke normalisert.
            // Navneoppslaget trimmer og lowercaser selv.
            return record.TaxonRank;
        }

        // Er forfatteren gjettet ut av artsnavnet, forteller antall ledd som blir igjen
        // hvilken rang navnet har.
        if (!string.IsNullOrWhiteSpace(record.ScientificName)
            && !string.IsNullOrWhiteSpace(authorString)
            && record.ScientificNameAuthor?.ToLowerInvariant() != authorString)
        {
            var ledd = record.ScientificName.ToLowerInvariant()
                .Replace(authorString.ToLowerInvariant(), string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (ledd.Length == 2) return "species";
            if (ledd.Length > 2) return "subspecies";
        }

        // Ellers: det laveste utfylte rangfeltet bestemmer. Merk at order sjekkes etter
        // phylum — en kjent skjevhet, bevart.
        if (ErUtfylt(record.Subspecies)) return "subspecies";
        if (ErUtfylt(record.Species)) return "species";
        if (ErUtfylt(record.Genus)) return "genus";
        if (ErUtfylt(record.Family)) return "family";
        if (ErUtfylt(record.Class)) return "class";
        if (ErUtfylt(record.Phylum)) return "phylum";
        if (ErUtfylt(record.Order)) return "order";
        if (ErUtfylt(record.Kingdom)) return "kingdom";

        return null;
    }

    /// <summary>
    /// Utfylt med noe annet enn en plassholder som «sp» eller «indet».
    /// </summary>
    private static bool ErUtfylt(string? verdi) =>
        !string.IsNullOrWhiteSpace(TaxonomyHelper.StripRankPlaceholder(verdi));

    /// <summary>
    /// Feilteksten Artskart 2 bygger for et navn som ikke kunne bestemmes: artsnavn,
    /// forfatter og hele rangstien.
    /// </summary>
    private static string Feilkontekst(CacheRecord record) =>
        $"Could match taxon for sn:{record.ScientificName} autor:{record.ScientificNameAuthor} "
        + $"path: {record.Kingdom} {record.Phylum} {record.Class} {record.Order} "
        + $"{record.Family} {record.Genus} {record.Species} {record.Subspecies} ";
}

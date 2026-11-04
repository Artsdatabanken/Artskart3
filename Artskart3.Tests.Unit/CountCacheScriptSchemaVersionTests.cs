using System.Text.RegularExpressions;
using Artskart3.Core.Application.Services;
using Artskart3.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Byggeskriptene speiler C#-registeret, og alt er håndkopiert: <c>@SchemaVersion</c>,
/// uttrykket som fyller <c>SourceRows</c>, og hele dimensjonslista.
///
/// Spriker @SchemaVersion, nekter oppslaget å bruke en buffer som er helt i orden —
/// irriterende, men trygt. Spriker SourceRows-uttrykket, sammenligner oppslaget to
/// tall som er målt på hver sin måte, og bufferen slår seg av uten at noe er galt.
/// Begge er stille feil som bare viser seg som «bufferen virker ikke», og ingen
/// kompilator fanger dem. Derfor leses skriptene her, fra samme embeddede ressurser
/// som migrasjonen BuildCountCaches kjører.
///
/// DIMENSJONSLISTA ER DEN VIKTIGSTE AV DE TRE, og den ble lenge ikke kontrollert.
///
/// Pargruppe-id-en er POSISJONELL: både BuildPairIds og skriptets
/// ROW_NUMBER() OVER (ORDER BY a.Id, b.Id) nummererer parene 1, 2, 3 … i samme
/// rekkefølge, og ingenting i de lagrede radene sier hvilket par en id står for.
/// Legges en dimensjon til med id 14, forskyves hvert par etter (1, 13) med én:
/// C# ber om 14 for (2, 3) = Regstatus+Atferd, mens skriptet lagret 14 for
/// (2, 4) = Regstatus+Funntype. Riktige tall for feil par, uten feilmelding.
///
/// @SchemaVersion skal fange nettopp det — men bare hvis den IKKE stemmer. Og
/// rekkefølgen en utvikler møter er: legg til dimensjonen i C#, se
/// versjonstesten feile, lim det nye tallet inn i begge skriptene, se grønt. Da
/// er signalet borte, og om dimensjonsraden faktisk ble lagt til i skriptenes
/// #dim-liste var det ingenting som sjekket. Derfor sammenlignes lista her:
/// å lime inn et tall gjør ikke lenger testen grønn.
/// </summary>
public class CountCacheScriptSchemaVersionTests
{
    private const string AreaScript = "Artskart3.Infrastructure.Scripts.BuildAreaCountCache.sql";
    private const string LocationScript = "Artskart3.Infrastructure.Scripts.BuildLocationCountCache.sql";

    [Theory]
    [InlineData(AreaScript)]
    [InlineData(LocationScript)]
    public void Skriptets_schemaversion_stemmer_med_koden(string ressurs)
    {
        var sql = LesSkript(ressurs);

        var treff = Regex.Match(sql, @"DECLARE\s+@SchemaVersion\s+INT\s*=\s*(-?\d+)\s*;");

        treff.Success.Should().BeTrue($"{ressurs} må deklarere @SchemaVersion som en literal");
        int.Parse(treff.Groups[1].Value).Should().Be(
            AreaCountCacheDimensions.SchemaVersion,
            "skriptet bygger bøtte-id-ene, og oppslaget nekter å bruke en buffer med annen versjon");
    }

    /// <summary>
    /// SourceRows må måles med samme uttrykk på begge sider. Sammenligningen er
    /// normalisert for mellomrom og linjeskift, ikke for innhold.
    /// </summary>
    [Theory]
    [InlineData(AreaScript)]
    [InlineData(LocationScript)]
    public void Skriptet_leser_sourcerows_med_samme_uttrykk_som_oppslaget(string ressurs)
    {
        var sql = Normaliser(LesSkript(ressurs));

        var forventet = Normaliser(CountCacheSourceRows.Sql)
            .Replace("AS Value", string.Empty, StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();

        sql.Should().Contain(forventet,
            "oppslaget sammenligner SourceRows med sitt eget uttrykk, og to ulike målinger " +
            "ville slått bufferen av uten at dataene har endret seg");
    }

    /// <summary>
    /// Områdeskriptets dimensjonsliste skal være registeret, rad for rad.
    ///
    /// Id OG navn, i id-rekkefølge: en id som mangler forskyver pargruppe-id-ene,
    /// og et navn som spriker betyr at de to listene har gått fra hverandre selv om
    /// antallet stemmer.
    ///
    /// Pargruppe-id-ene trenger ingen egen kontroll: begge sider utleder dem av
    /// nøyaktig denne lista med samme algoritme, så stemmer lista, stemmer de.
    /// </summary>
    [Fact]
    public void Omraadeskriptets_dimensjoner_er_registeret()
    {
        var skript = LesDimensjoner(AreaScript).Select(d => (d.Id, d.Navn)).ToList();

        skript.Should().Equal(Registeret(),
            "pargruppe-id-ene er posisjonelle, så en manglende eller omdøpt dimensjon " +
            "gir riktige tall for feil par");
    }

    /// <summary>
    /// Lokasjonsskriptet har én dimensjon mer enn registeret: id 0, «ingen filter».
    /// Den finnes ikke i AreaCountCacheDimensions fordi områdebufferen aldri spørres
    /// uten filter — men for lokasjonene er det det tregeste kallet som finnes.
    /// Resten skal være registeret.
    /// </summary>
    [Fact]
    public void Lokasjonsskriptets_dimensjoner_er_registeret_pluss_ufiltrert()
    {
        var skript = LesDimensjoner(LocationScript);

        skript.Select(d => d.Id).Should().Contain(LocationCountCacheService.NoFilterDimensionId,
            "det ufiltrerte oppslaget har sin egen dimensjon, og uten den svarer bufferen " +
            "ikke på det tregeste kallet vi har");

        skript.Where(d => d.Id != LocationCountCacheService.NoFilterDimensionId)
            .Select(d => (d.Id, d.Navn))
            .Should().Equal(Registeret());
    }

    /// <summary>
    /// De to skriptene må bøtte IDENTISK for dimensjonene de deler.
    ///
    /// Lokasjonsbufferen låner flerverdi-bøttene fra områdebufferens
    /// AreaCountCacheBucketMember, og for de andre dimensjonene må bøtte-id-ene
    /// betyr det samme i begge tabellene. Står det to ulike uttrykk, er det to
    /// nummereringer som skal gi samme tall — og lokasjonsskriptets egen kommentar
    /// sier at uttrykket er «ordrett det samme», noe ingen kontrollerte.
    /// </summary>
    [Fact]
    public void Skriptene_bruker_identiske_uttrykk_for_dimensjonene_de_deler()
    {
        var omraade = LesDimensjoner(AreaScript)
            .ToDictionary(d => d.Id, d => d.Uttrykk);

        var lokasjon = LesDimensjoner(LocationScript)
            .Where(d => d.Id != LocationCountCacheService.NoFilterDimensionId)
            .ToDictionary(d => d.Id, d => d.Uttrykk);

        lokasjon.Should().BeEquivalentTo(omraade,
            "to uttrykk som skal gi samme bøtte er to uttrykk som en dag ikke gjør det");
    }

    /// <summary>
    /// Antallet par skriptet melder under kjøring skal være det faktiske antallet.
    /// Meldingen står i en RAISERROR midt i en kjøring på rundt ti minutter, og er
    /// det eneste operatøren har å lese imens.
    /// </summary>
    [Fact]
    public void Omraadeskriptet_melder_riktig_antall_par()
    {
        var sql = LesSkript(AreaScript);

        var treff = Regex.Match(sql, @"Nivaa 2 \((\d+) par\)");

        treff.Success.Should().BeTrue("skriptet skal melde hvor mange par det bygger");
        int.Parse(treff.Groups[1].Value).Should().Be(AreaCountCacheDimensions.PairCount);
    }

    private static List<(int Id, string Navn)> Registeret() =>
        AreaCountCacheDimensions.All
            .OrderBy(d => d.Id)
            .Select(d => ((int)d.Id, d.Name))
            .ToList();

    /// <summary>
    /// Plukker dimensjonslista ut av et skript. Formen er fast:
    /// <c>INSERT #dim VALUES</c> fulgt av rader på formen
    /// <c>(id, 'Navn', N'uttrykk'),</c> til første semikolon.
    ///
    /// Linjekommentarer fjernes først. Lista har forklarende kommentarer mellom
    /// radene, og en kommentar som nevner en dimensjon skal ikke leses som en rad.
    /// </summary>
    private static List<(int Id, string Navn, string Uttrykk)> LesDimensjoner(string ressurs)
    {
        const string start = "INSERT #dim VALUES";

        var sql = LesSkript(ressurs);
        var fra = sql.IndexOf(start, StringComparison.Ordinal);

        fra.Should().BeGreaterThanOrEqualTo(0, $"{ressurs} må deklarere dimensjonene i #dim");

        var utenKommentarer = Regex.Replace(sql[(fra + start.Length)..], @"--[^\n]*", string.Empty);
        var slutt = utenKommentarer.IndexOf(';', StringComparison.Ordinal);

        slutt.Should().BeGreaterThanOrEqualTo(0, "INSERT-en må avsluttes med semikolon");

        return Regex.Matches(utenKommentarer[..slutt], @"\(\s*(\d+)\s*,\s*'([^']*)'\s*,\s*N'(.*?)'\s*\)")
            .Select(m => (
                Id: int.Parse(m.Groups[1].Value),
                Navn: m.Groups[2].Value.Trim(),
                Uttrykk: m.Groups[3].Value))
            .OrderBy(d => d.Id)
            .ToList();
    }

    private static string LesSkript(string ressurs)
    {
        var assembly = typeof(CountCacheSourceRows).Assembly;
        using var stream = assembly.GetManifestResourceStream(ressurs);

        stream.Should().NotBeNull($"{ressurs} må være EmbeddedResource i Artskart3.Infrastructure");

        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static string Normaliser(string sql) =>
        Regex.Replace(sql, @"\s+", " ");
}

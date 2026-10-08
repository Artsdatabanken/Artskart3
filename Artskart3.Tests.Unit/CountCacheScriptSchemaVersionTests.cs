using System.Text.RegularExpressions;
using Artskart3.Core.Application.Services;
using Artskart3.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Byggeskriptene speiler to verdier fra C#, og begge er håndkopierte literaler:
/// <c>@SchemaVersion</c> og uttrykket som fyller <c>SourceRows</c>.
///
/// Spriker @SchemaVersion, nekter oppslaget å bruke en buffer som er helt i orden —
/// irriterende, men trygt. Spriker SourceRows-uttrykket, sammenligner oppslaget to
/// tall som er målt på hver sin måte, og bufferen slår seg av uten at noe er galt.
/// Begge er stille feil som bare viser seg som «bufferen virker ikke», og ingen
/// kompilator fanger dem. Derfor leses skriptene her, fra samme embeddede ressurser
/// som migrasjonen BuildCountCaches kjører.
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

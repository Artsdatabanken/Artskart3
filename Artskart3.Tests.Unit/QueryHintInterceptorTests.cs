using Artskart3.Infrastructure.Data.Interceptors;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vokter hintene som legges på ferdig generert SQL.
///
/// Hintet kan ikke uttrykkes i LINQ, så det settes inn ved å skrive om
/// kommandoteksten. Den omskrivingen er lett å få subtilt feil — særlig
/// sammenslåingen, siden SQL Server bare tillater ETT OPTION-ledd per setning.
/// </summary>
public class QueryHintInterceptorTests
{
    private const string LokasjonsSql = """
        -- locations-search

        SELECT [o].[LocationId] FROM [Observation] AS [o]
        """;

    private const string ListevisningsSql = """
        -- listview-search

        SELECT [o].[Id] FROM [Observation] AS [o]
        """;

    private const string BeggeTagger = """
        -- listview-search
        -- locations-search

        SELECT 1
        """;

    [Fact]
    public void Lokasjonssoeket_faar_recompile()
    {
        QueryHintInterceptor.Rewrite(LokasjonsSql).Should().EndWith("OPTION (RECOMPILE)");
    }

    [Fact]
    public void Listevisningen_faar_recompile()
    {
        QueryHintInterceptor.Rewrite(ListevisningsSql).Should().EndWith("OPTION (RECOMPILE)");
    }

    /// <summary>
    /// Begge taggene gir RECOMPILE i dag. Uten dedupliseringen ville en spørring
    /// som bar begge fått OPTION (RECOMPILE, RECOMPILE) — en syntaksfeil.
    /// </summary>
    [Fact]
    public void Begge_taggene_gir_ett_recompile_ikke_to()
    {
        var resultat = QueryHintInterceptor.Rewrite(BeggeTagger);

        resultat.Should().EndWith("OPTION (RECOMPILE)");
        resultat.Should().NotContain("RECOMPILE, RECOMPILE");
    }

    /// <summary>
    /// Ett OPTION-ledd per setning. Skulle to tagger en dag gi ULIKE hint, må de
    /// slås sammen i ett ledd — ikke skrives som to.
    /// </summary>
    [Fact]
    public void Aldri_mer_enn_ett_OPTION_ledd()
    {
        QueryHintInterceptor.Rewrite(BeggeTagger).Split("OPTION (").Should().HaveCount(2);
    }

    [Fact]
    public void Utagget_spoerring_roeres_ikke()
    {
        const string sql = "SELECT [o].[Id] FROM [Observation] AS [o]";

        QueryHintInterceptor.Rewrite(sql).Should().Be(sql);
    }

    /// <summary>
    /// EF kan sende samme kommando gjennom interceptoren flere ganger ved retry
    /// på transiente feil, og to OPTION-ledd er en syntaksfeil.
    /// </summary>
    [Fact]
    public void Kjoert_to_ganger_gir_samme_resultat()
    {
        var en = QueryHintInterceptor.Rewrite(LokasjonsSql);

        QueryHintInterceptor.Rewrite(en).Should().Be(en);
    }

    /// <summary>
    /// OPTION må stå aller sist i setningen, etter et eventuelt OFFSET/FETCH-ledd.
    /// </summary>
    [Fact]
    public void Avsluttende_semikolon_havner_ikke_foran_OPTION()
    {
        var resultat = QueryHintInterceptor.Rewrite(ListevisningsSql + ";");

        resultat.Should().EndWith("OPTION (RECOMPILE)");
        resultat.Should().NotContain(";");
    }

    /// <summary>
    /// Et tvunget planvalg ble prøvd og forkastet: det var 10x raskere på brede
    /// filtre, men 260x tregere på smale — katalognr:tung gikk fra 9 ms til
    /// 13 007 ms i Standard-kjøringen. Kommer et slikt hint tilbake, skal det
    /// være et bevisst valg med nye målinger, ikke noe som sniker seg inn igjen.
    /// </summary>
    [Fact]
    public void Ingen_tvungne_planvalg()
    {
        const string sql = """
            -- locations-search

            SELECT 1 FROM [Location] AS [l]
            INNER JOIN [Observation] AS [o] ON [l].[Id] = [o].[LocationId]
            """;

        var resultat = QueryHintInterceptor.Rewrite(sql);

        resultat.Should().NotContain("FORCE ORDER");
        resultat.Should().NotContain("LOOP JOIN");
        resultat.Should().Contain("INNER JOIN [Observation]");
    }
}

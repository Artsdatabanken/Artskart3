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
    /// INGEN SPØRRING SKAL FÅ ET TVUNGET PLANVALG HER.
    ///
    /// Det er prøvd to ganger og forkastet to ganger. For lokasjonssøket var en
    /// tvungen loop join 10x raskere på brede filtre og 260x tregere på smale —
    /// katalognr:tung gikk fra 9 til 13 007 ms. For polygonhentingen var den en
    /// stor gevinst i snitt, men 68 708 ms mot 12 ms når to filtre til sammen
    /// ikke traff noe.
    ///
    /// Polygonhentingen har ikke lenger noe hint i det hele tatt: den svarer fra
    /// PolygonLocationStore, og databasestien brukes bare mens datasettet
    /// bygges ved oppstart. Der er «aldri katastrofal» verdt mer enn «rask i
    /// snitt».
    ///
    /// Kommer et planhint tilbake, skal det være et bevisst valg med målinger
    /// fra HELE Full-nivået bak seg. Enkeltmålinger lyver her; det var nettopp
    /// slik det slapp inn begge gangene.
    /// </summary>
    [Fact]
    public void Ingen_spoerring_faar_tvunget_planvalg()
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

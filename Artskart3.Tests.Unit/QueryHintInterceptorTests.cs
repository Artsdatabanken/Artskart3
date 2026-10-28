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
    private const string ListevisningsSql = """
        -- listview-search

        SELECT [o].[Id] FROM [Observation] AS [o]
        """;

    /// <summary>
    /// Én kjent tagg sammen med en ukjent. Den ukjente skal ikke gi noe hint, og
    /// den kjente skal gi nøyaktig ett OPTION-ledd.
    /// </summary>
    private const string KjentOgUkjentTagg = """
        -- listview-search
        -- en-tagg-uten-hint

        SELECT 1
        """;

    private const string LokasjonstellingSql = """
        -- listview-search
        -- no-grant-feedback

        SELECT COUNT(*) FROM (SELECT TOP(@p) 1 AS empty FROM [ObservationEntityIndex] AS [o]) AS [o0]
        """;

    private const string BeggeHintene =
        "OPTION (RECOMPILE, USE HINT('DISABLE_BATCH_MODE_MEMORY_GRANT_FEEDBACK', 'DISABLE_ROW_MODE_MEMORY_GRANT_FEEDBACK'))";

    [Fact]
    public void Listevisningen_faar_recompile()
    {
        QueryHintInterceptor.Rewrite(ListevisningsSql).Should().EndWith("OPTION (RECOMPILE)");
    }

    [Fact]
    public void Listevisningen_faar_ikke_hintet_mot_minnetilbakemelding()
    {
        QueryHintInterceptor.Rewrite(ListevisningsSql).Should().NotContain("USE HINT");
    }

    /// <summary>
    /// To hint på samme spørring skal bli ett OPTION-ledd med begge, kommaskilt.
    /// To ledd i én setning er en syntaksfeil.
    /// </summary>
    [Fact]
    public void To_tagger_gir_begge_hintene_i_ett_ledd()
    {
        var resultat = QueryHintInterceptor.Rewrite(LokasjonstellingSql);

        resultat.Should().EndWith(BeggeHintene);
        resultat.Split("OPTION (").Should().HaveCount(2);
    }

    /// <summary>
    /// Rekkefølgen i OPTION-leddet kommer fra hinttabellen, ikke fra rekkefølgen
    /// taggene ble lagt på i LINQ.
    /// </summary>
    [Fact]
    public void Taggenes_rekkefoelge_endrer_ikke_OPTION_leddet()
    {
        const string omvendt = """
            -- no-grant-feedback
            -- listview-search

            SELECT 1
            """;

        QueryHintInterceptor.Rewrite(omvendt).Should().EndWith(BeggeHintene);
    }

    [Fact]
    public void To_hint_kjoert_to_ganger_gir_samme_resultat()
    {
        var en = QueryHintInterceptor.Rewrite(LokasjonstellingSql);

        QueryHintInterceptor.Rewrite(en).Should().Be(en);
    }

    [Fact]
    public void Ukjent_tagg_ved_siden_av_en_kjent_endrer_ingenting()
    {
        var resultat = QueryHintInterceptor.Rewrite(KjentOgUkjentTagg);

        resultat.Should().EndWith("OPTION (RECOMPILE)");
        resultat.Should().NotContain("RECOMPILE, RECOMPILE");
    }

    /// <summary>
    /// Ett OPTION-ledd per setning, uansett hvor mange tagger spørringen bærer.
    /// Flere hint MÅ slås sammen i ett ledd — to ledd er en syntaksfeil, og det er
    /// hele grunnen til at hintene står i én tabell i én interceptor.
    /// </summary>
    [Fact]
    public void Aldri_mer_enn_ett_OPTION_ledd()
    {
        QueryHintInterceptor.Rewrite(KjentOgUkjentTagg).Split("OPTION (").Should().HaveCount(2);
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
        var en = QueryHintInterceptor.Rewrite(ListevisningsSql);

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
            -- listview-search

            SELECT 1 FROM [Location] AS [l]
            INNER JOIN [Observation] AS [o] ON [l].[Id] = [o].[LocationId]
            """;

        var resultat = QueryHintInterceptor.Rewrite(sql);

        resultat.Should().NotContain("FORCE ORDER");
        resultat.Should().NotContain("LOOP JOIN");
        resultat.Should().Contain("INNER JOIN [Observation]");
    }
}

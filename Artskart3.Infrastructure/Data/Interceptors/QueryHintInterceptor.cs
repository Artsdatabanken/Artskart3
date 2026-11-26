using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Artskart3.Infrastructure.Data.Interceptors;

/// <summary>
/// Legger OPTION-hint på spørringer merket med en kjent tagg.
///
/// Hintene kan ikke uttrykkes i LINQ — EF Core genererer ikke OPTION-ledd — så
/// spørringen merkes med TagWith() og hintet settes på her.
///
/// Én interceptor for alle hint, ikke én per hint: SQL Server tillater bare ETT
/// OPTION-ledd per setning. To interceptorer som hver skrev sitt ville gitt en
/// syntaksfeil på en spørring som bar begge taggene. Her slås de sammen.
/// </summary>
public sealed class QueryHintInterceptor : DbCommandInterceptor
{
    /// <summary>
    /// Listevisningen. Plan per kjøring koster ~20 ms, men sparer over ett sekund
    /// på de trege filtrene fordi optimizeren får se de faktiske verdiene.
    /// </summary>
    public const string RecompileTag = "listview-search";

    /// <summary>
    /// Slår av minnetilbakemelding (memory grant feedback) i både batch- og
    /// radmodus. Query Store tar vare på tilbakemeldingen også på tvers av
    /// RECOMPILE, og etter noen kjøringer med ulike verdier krymper den grantet
    /// mot det de minste kallene brukte. De største spiller da til tempdb.
    ///
    /// Brukes av lokasjonstellingen, der Flow Distinct under TOP da mister den
    /// tidlige utgangen. Målt 27.–28.10.2026 mot Artskart3TestMigrations:
    /// atferd:tung + opprinnelse:tung 0,2 → 4 s. Radmodus var årsaken; med begge
    /// slått av holder den ~0,2 s. Prisen er at grantet blir stående på estimatet.
    /// De andre spørringene får også krympet grant uten å bli tregere, og skal ikke
    /// ha taggen.
    /// </summary>
    public const string NoGrantFeedbackTag = "no-grant-feedback";

    /// <summary>
    /// Slår av radmålet (row goal). Brukes på områdegrenene i listevisningen og galleriet
    /// når filteret har kolonner IX_OEI_AreaListView og IX_OEI_MediaListView ikke dekker.
    /// Da må hver rad slås opp i klyngeindeksen, og optimizeren antar at treffene er like
    /// vanlige i området som i hele landet. Målt 16.11.2026 mot Artskart3TestMigrations,
    /// topp 40 i Troms: «ikke påvist» 1 770 → 40 ms, med columnstore i stedet for 702 000
    /// oppslag, og «påvist» 0–5 → 65–83 ms.
    /// </summary>
    public const string NoRowGoalTag = "no-row-goal";

    /// <summary>
    /// Tvinger områdetellingene til IX_OEI_Columnstore. Brukes når de er avgrenset til bestemte
    /// områder og en periode: da kan IX_OEI_AreaListView seeke på et datointervall, og planen
    /// fra første kall gjenbrukes for helt andre verdier. Målt 16.11.2026 mot
    /// Artskart3TestMigrations, Trøndelag + Farsund + 1970–2026 + fugler på zoomnivå 2:
    /// 2 500 ms med gjenbrukt seriell plan, 155–180 ms med columnstore.
    ///
    /// RECOMPILE løste tidene, men grantet ble beregnet fra estimatene ved hver kjøring: opptil
    /// 3 GB for spørringer som brukte 10 MB. Med columnstore og bufret plan var det 56 MB.
    ///
    /// Aliaset [o] er det EF gir hovedtabellen i spørringen. Endres det, feiler spørringen.
    /// </summary>
    public const string CountColumnstoreTag = "count-columnstore";

    /// <summary>
    /// Tagg → hintet den utløser. Rekkefølgen her blir rekkefølgen i OPTION-leddet.
    ///
    /// Flere hint på samme spørring slås sammen til ett OPTION-ledd, for to ledd i
    /// én setning er en syntaksfeil. QueryHintInterceptorTests vokter det.
    ///
    /// POLYGONHENTINGEN HADDE EN GANG SITT EGET HINT — DEN ER BORTE MED VILJE
    /// Den ble drevet fra Location med tvungen loop join, og det var en stor
    /// gevinst på brede filtre (regstatus:tung 10 799 → 1 135 ms). Men den var
    /// katastrofal når to filtre til sammen ikke traff noe: 68 708 ms mot 12 ms
    /// uten hintet. Det ble først forsøkt dempet med RECOMPILE, som bare flyttet
    /// problemet, og deretter med en selektivitetssonde som virket.
    ///
    /// Alt det er nå overflødig: polygonsøket svarer fra PolygonLocationStore,
    /// og databasestien brukes bare i de ~37 sekundene datasettet bygges ved
    /// oppstart. I det vinduet er «aldri katastrofal» verdt mer enn «rask i
    /// snitt», og den uhintede planen er nettopp det.
    /// </summary>
    private static readonly (string Tag, string Hint)[] Hints =
    [
        (RecompileTag, "RECOMPILE"),
        (NoGrantFeedbackTag, "USE HINT('DISABLE_BATCH_MODE_MEMORY_GRANT_FEEDBACK', 'DISABLE_ROW_MODE_MEMORY_GRANT_FEEDBACK')"),
        (NoRowGoalTag, "USE HINT('DISABLE_OPTIMIZER_ROWGOAL')"),
        (CountColumnstoreTag, "TABLE HINT([o], INDEX([IX_OEI_Columnstore]))"),
    ];

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        ApplyHints(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ApplyHints(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void ApplyHints(DbCommand command)
    {
        command.CommandText = Rewrite(command.CommandText);
    }

    /// <summary>
    /// Selve omskrivingen. Offentlig fordi hintene er verdt å teste for seg —
    /// de settes inn med tekstbytte på generert SQL, og en stille bom der koster
    /// mer enn et litt bredere API.
    /// </summary>
    public static string Rewrite(string text)
    {
        // Idempotent: EF kan sende samme kommando gjennom interceptoren flere
        // ganger (retry ved transient feil), og to OPTION-ledd er en syntaksfeil.
        if (text.Contains("OPTION (", StringComparison.Ordinal))
            return text;

        // TagWith legger taggen inn som en SQL-kommentar øverst i spørringen.
        var treff = Hints
            .Where(h => text.Contains(h.Tag, StringComparison.Ordinal))
            .Select(h => h.Hint)
            .Distinct()
            .ToArray();

        if (treff.Length == 0)
            return text;

        // OPTION må stå aller sist i setningen, etter et eventuelt
        // OFFSET/FETCH-ledd. Et avsluttende semikolon ville kommet i veien.
        var trimmet = text.TrimEnd();
        if (trimmet.EndsWith(';'))
            trimmet = trimmet[..^1].TrimEnd();

        return trimmet + Environment.NewLine
            + "OPTION (" + string.Join(", ", treff) + ")";
    }
}

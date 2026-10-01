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
    /// Lokasjonssøket og polygonhentingen.
    ///
    /// Samme begrunnelse som listevisningen, men den ble funnet på den dyre
    /// måten. Riktig plan avhenger fullstendig av hvor selektivt filteret er, og
    /// de to ytterpunktene vil ha motsatt plan:
    ///
    ///   Sak                   Tvunget loop join fra Location   RECOMPILE
    ///   katalognr (9 id-er)                        2328 ms         8 ms
    ///   atferd:lett (39k obs)                      3217 ms       509 ms
    ///   ufiltrert, stort utsnitt                    999 ms      1780 ms
    ///   ufiltrert, lite utsnitt                     107 ms       555 ms
    ///   kommune Oslo, stort utsnitt                2310 ms      4425 ms
    ///
    /// En tvunget plan var 10x raskere på de brede filtrene og ble først lagt
    /// inn — men den er 260x tregere på de smale, og Standard-kjøringen viste
    /// katalognr:tung gå fra 9 ms til 13 007 ms. RECOMPILE taper aldri mer enn
    /// rundt det dobbelte, og den asymmetrien er hele poenget: optimizeren får
    /// se de faktiske verdiene og velge selv.
    ///
    /// Spørringsformen viste seg å ikke bety noe. Med RECOMPILE gir join fra
    /// Location og IN-subspørring samme tall (1885 mot 1780 ufiltrert, 6 mot 8
    /// på katalognr), fordi optimizeren normaliserer dem. Derfor står EF-ens
    /// naturlige form igjen.
    /// </summary>
    public const string LocationsTag = "locations-search";

    /// <summary>
    /// Tagg → hintet den utløser. Rekkefølgen her blir rekkefølgen i OPTION-leddet.
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
        (LocationsTag, "RECOMPILE"),
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

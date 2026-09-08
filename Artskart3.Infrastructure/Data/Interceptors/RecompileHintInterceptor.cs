using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Artskart3.Infrastructure.Data.Interceptors;

/// <summary>
/// Legger OPTION (RECOMPILE) på spørringer som er merket med <see cref="Tag"/>.
///
/// HVORFOR: listevisningens spørring har ett planformat, men hvilken plan som er
/// riktig avhenger helt av parameterverdien. Kommune Farsund har 2 169 747
/// observasjoner, Sørreisa har 6 782. Optimalisereren kompilerer én plan for den
/// første verdien den ser og gjenbruker den for alle andre — og planene er ikke
/// utbyttbare:
///
///   Plan kompilert for   Kjørt med    Tid
///   Sørreisa             Sørreisa      26 ms
///   Sørreisa             Farsund   11 265 ms
///   Farsund              Farsund       19 ms
///   Farsund              Sørreisa   1 291 ms
///
/// Med RECOMPILE kompileres planen per kjøring, med den faktiske verdien:
/// 23 ms og 10 ms i de to tilfellene over.
///
/// Det samme gjelder institusjon (32 til 29,2M observasjoner) og datasett
/// (1 til 29,2M) — alle filtre der treffmengden spenner flere størrelsesordener.
///
/// KOSTNADEN er kompilering per kjøring. Målt på de raske tilfellene:
///   Uten filter            0,3 ms -> 0,9 ms
///   Institusjon lettest    0,2 ms -> 1,0 ms
///   Kategori               0,3 ms -> 1,0 ms
///   Takson (hierarki)      0,7 ms -> 6,0 ms
/// Kompileringen skalerer med hvor sammensatt spørringen er, så et filter med
/// mange ledd koster mer enn de 5 ms taksonspørringen viser. Målingene er gjort
/// på en ledig maskin; under samtidig last bruker rekompilering CPU som en
/// cachet plan ikke gjør.
///
/// Hintet legges på her og ikke i LINQ-en fordi EF Core ikke kan generere
/// OPTION-hint. Alternativet var å skrive spørringen som rå SQL, og da måtte
/// projeksjonen med joins og subselects vedlikeholdes for hånd.
/// </summary>
public sealed class RecompileHintInterceptor : DbCommandInterceptor
{
    /// <summary>Settes med TagWith() på spørringen som skal ha hintet.</summary>
    public const string Tag = "listview-search";

    private const string Hint = "OPTION (RECOMPILE)";

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        ApplyHint(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ApplyHint(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void ApplyHint(DbCommand command)
    {
        var text = command.CommandText;

        // TagWith legger taggen inn som en SQL-kommentar øverst i spørringen.
        if (!text.Contains(Tag, StringComparison.Ordinal))
            return;

        // Idempotent: EF kan kjøre samme kommando gjennom interceptoren flere
        // ganger (retry ved transient feil), og to hint er en syntaksfeil.
        if (text.Contains(Hint, StringComparison.Ordinal))
            return;

        // OPTION må stå aller sist i setningen, etter et eventuelt
        // OFFSET/FETCH-ledd. Et avsluttende semikolon ville kommet i veien.
        var trimmed = text.TrimEnd();
        if (trimmed.EndsWith(';'))
            trimmed = trimmed[..^1].TrimEnd();

        command.CommandText = trimmed + Environment.NewLine + Hint;
    }
}

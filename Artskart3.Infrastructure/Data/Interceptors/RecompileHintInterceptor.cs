using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Artskart3.Infrastructure.Data.Interceptors;

/// <summary>
/// Legger OPTION (RECOMPILE) på spørringer som er merket med <see cref="Tag"/>.
///
/// RECOMPILE kompilerer planen per kjøring, som gjør at riktig plan blir valgt i forhold til hvilke data man skal hente.
/// Kommer med en kostnad på ca 20ms per spørring, men forbedrer trege spørringer med opp mot over 1 sek. 
///
/// Hintet legges på her og ikke i LINQ-en fordi EF Core ikke kan generere
/// OPTION-hint.
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

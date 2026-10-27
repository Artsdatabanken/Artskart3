using System.Diagnostics;
using Artskart3.Infrastructure.Maintenance;
using Microsoft.Data.SqlClient;

namespace Artskart3.Workers.Maintenance;

/// <summary>
/// Hangfire recurring job som kjører før harvest for å sikre at referansedata er oppdatert.
///
/// Ansvarsområder:
/// - Taksonomi-sync: Oppdaterer taksontabellene fra Artsdatabankens taksonomi-API
///   (erstatter TaxonomyWash som i dag kjører i artskart2)
/// - Områdeoppdateringer: Oppdaterer kommune-/fylkesgrenser og verneområder
/// - Vedlikeholdsoppgaver: Datarydding og andre planlagte vedlikeholdsjobber
/// - Columnstore-vedlikehold: REORGANIZE av IX_OEI_Columnstore etter ImportJob
///
/// COLUMNSTORE-VEDLIKEHOLDET ER IMPLEMENTERT. Taksonomi-sync og områdeoppdateringer er
/// det ikke — de hører til harvest.
///
/// Columnstore-vedlikehold (kjøres etter ImportJob, ikke før):
///
///     ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex
///         REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON);
///
/// REORGANIZE er online og inkrementell — den slår sammen små rowgroups og fjerner
/// slettede rader fysisk. COMPRESS_ALL_ROW_GROUPS tvinger også gjenværende
/// delta-rowgroups til å komprimeres, noe som er nødvendig når en daglig batch
/// var mindre enn 102 400 rader.
///
/// Bruk REORGANIZE, ikke REBUILD. En full rebuild av 192M rader tar 10-30 minutter
/// og hører ikke hjemme i en daglig jobb. Rebuild er kun nødvendig når andelen
/// slettede rader passerer ~20 %, som kan overvåkes med:
///
///     SELECT SUM(total_rows), SUM(deleted_rows)
///     FROM sys.dm_db_column_store_row_group_physical_stats
///     WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex');
/// </summary>
public class DatabaseMaintenanceJob
{
    /// <summary>
    /// Columnstore-indeksen på <c>ObservationEntityIndex</c>. Navnet er gitt i
    /// migrasjon 20260820140154 — EF Core modellerer ikke columnstore, så den finnes
    /// bare i SQL.
    /// </summary>
    private const string ColumnstoreIndex = "IX_OEI_Columnstore";

    private readonly MaintenanceOptions _innstillinger;
    private readonly ILogger<DatabaseMaintenanceJob> _logger;

    public DatabaseMaintenanceJob(MaintenanceOptions innstillinger, ILogger<DatabaseMaintenanceJob> logger)
    {
        _innstillinger = innstillinger;
        _logger = logger;
    }

    /// <summary>
    /// Kjører columnstore-vedlikeholdet. Taksonomi-sync og områdeoppdateringer er
    /// fortsatt ikke implementert — de hører til harvest, ikke til importen.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_innstillinger.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Indeksen finnes ikke i en testbase som bare har tabellene. Da er det
        // ingenting å vedlikeholde, og det er ikke en feil.
        await using var finnes = new SqlCommand(
            $"SELECT COUNT(*) FROM sys.indexes WHERE name = '{ColumnstoreIndex}';", connection);

        if ((int)(await finnes.ExecuteScalarAsync(cancellationToken))! == 0)
        {
            _logger.LogInformation(
                "{Indeks} finnes ikke i denne basen - ingenting å reorganisere", ColumnstoreIndex);

            return;
        }

        var før = await LesRadgrupper(connection, cancellationToken);

        var klokke = Stopwatch.StartNew();

        await using var reorganiser = new SqlCommand(
            $"""
             ALTER INDEX {ColumnstoreIndex} ON dbo.ObservationEntityIndex
                 REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON);
             """,
            connection)
        {
            CommandTimeout = _innstillinger.CommandTimeoutSeconds
        };

        await reorganiser.ExecuteNonQueryAsync(cancellationToken);

        klokke.Stop();

        var etter = await LesRadgrupper(connection, cancellationToken);

        _logger.LogInformation(
            "{Indeks} reorganisert på {Sekunder} s. Rader {RaderFør} → {RaderEtter}, "
            + "slettede {SlettetFør} → {SlettetEtter}, radgrupper {GrupperFør} → {GrupperEtter}",
            ColumnstoreIndex,
            Math.Round(klokke.Elapsed.TotalSeconds, 1),
            før.Rows,
            etter.Rows,
            før.Deleted,
            etter.Deleted,
            før.Groups,
            etter.Groups);

        // Andelen slettede rader er signalet planen peker på: over ~20 % hjelper ikke
        // REORGANIZE lenger, og da trengs en full REBUILD — som ikke hører i en daglig
        // jobb, og derfor bare logges her.
        if (etter.Rows > 0 && etter.Deleted * 100.0 / etter.Rows > 20)
        {
            _logger.LogWarning(
                "{Indeks} har {Andel} % slettede rader etter reorganisering. Over 20 % betyr at "
                + "en full REBUILD trengs — den tar 10-30 minutter og må planlegges.",
                ColumnstoreIndex,
                Math.Round(etter.Deleted * 100.0 / etter.Rows, 1));
        }
    }

    /// <summary>
    /// Tilstanden til radgruppene. Tallene er det eneste som viser om
    /// reorganiseringen gjorde noe.
    /// </summary>
    private static async Task<(long Rows, long Deleted, int Groups)> LesRadgrupper(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            SELECT ISNULL(SUM(CAST(total_rows AS BIGINT)), 0),
                   ISNULL(SUM(CAST(deleted_rows AS BIGINT)), 0),
                   COUNT(*)
            FROM sys.dm_db_column_store_row_group_physical_stats
            WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex');
            """,
            connection);

        await using var leser = await command.ExecuteReaderAsync(cancellationToken);

        if (!await leser.ReadAsync(cancellationToken)) return (0, 0, 0);

        return (leser.GetInt64(0), leser.GetInt64(1), leser.GetInt32(2));
    }
}

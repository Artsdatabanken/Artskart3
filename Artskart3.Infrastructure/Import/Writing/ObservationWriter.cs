using System.Data;
using System.Diagnostics;
using Artskart3.Core.Application.Import.Writing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// Innstillinger for skrivingen.
/// </summary>
public class ObservationWriterOptions
{
    public const string SectionName = "Import:Writer";

    /// <summary>Tilkoblingen til indexdatabasen.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Hvor mange poster som samles opp før en runde skrives. Planen foreslår 250 000:
    /// nok til at columnstore komprimerer radgruppene (minst 102 400), og lite nok til
    /// at en avbrutt kjøring ikke mister mye.
    /// </summary>
    public int BatchSize { get; set; } = 250_000;

    /// <summary>Tidsavbrudd for én bulk-kopiering, i sekunder. 0 er uendelig.</summary>
    public int BulkCopyTimeoutSeconds { get; set; } = 600;

    /// <summary>Tidsavbrudd for én publiseringssetning, i sekunder.</summary>
    public int CommandTimeoutSeconds { get; set; } = 1800;
}

/// <summary>
/// Skriver en bunt til indexdatabasen: tolv midlertidige tabeller, en bulk-kopiering
/// per tabell, og så sett-basert publisering i én transaksjon.
///
/// ALT GÅR PÅ ÉN TILKOBLING, og det er et krav, ikke en forenkling:
/// <c>#</c>-tabellene lever i sesjonen. Lukkes tilkoblingen, forsvinner de — som også
/// er oppryddingen.
///
/// HVORFOR MIDLERTIDIGE TABELLER OG IKKE ET STAGING-SKJEMA: ingen migrasjon å
/// vedlikeholde, ingen rester etter en krasj, og ingen fare for at to kjøringer tråkker
/// i hverandres rader. Prisen er at radene ikke kan undersøkes etterpå; det er en
/// avveining planen (4.4) tar, og den kan snus ved å bytte ut <c>CreateTableSql</c>.
///
/// BULK-KOPIERINGEN GÅR UTENFOR TRANSAKSJONEN. Staging-tabellene er private for
/// sesjonen, så det er ingenting å rulle tilbake: feiler kopieringen, kastes de når
/// tilkoblingen lukkes. Transaksjonen omfatter publiseringen, som er det som rører
/// ekte rader.
/// </summary>
public class ObservationWriter : IObservationWriter
{
    private readonly ObservationWriterOptions _innstillinger;
    private readonly ILogger<ObservationWriter> _logger;

    public ObservationWriter(ObservationWriterOptions innstillinger, ILogger<ObservationWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(innstillinger);

        if (string.IsNullOrWhiteSpace(innstillinger.ConnectionString))
        {
            throw new ArgumentException("Skrivingen trenger en tilkoblingsstreng.", nameof(innstillinger));
        }

        _innstillinger = innstillinger;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(
        ImportBatch batch,
        LocationIdCache? locationIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Count == 0)
        {
            return new PublishResult(0, 0, 0, TimeSpan.Zero);
        }

        var klokke = Stopwatch.StartNew();

        await using var connection = new SqlConnection(_innstillinger.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await OpprettStagingTabeller(connection, cancellationToken);

        var rader = await KopierTilStaging(connection, batch, cancellationToken);

        var nå = DateTime.UtcNow;

        await using var transaksjon = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Observasjonene og barna deres først, så de avledede tabellene — i samme
            // transaksjon, fordi en observasjon uten rader i ObservationEntityIndex er
            // usynlig for alle områdefiltre. Halvveis er ikke en gyldig tilstand.
            foreach (var steg in PublishSql.Steps.Concat(DerivedTableSql.Steps))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Kjør(connection, transaksjon, steg, nå, cancellationToken);
            }

            await transaksjon.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaksjon.RollbackAsync(CancellationToken.None);

            throw;
        }

        if (locationIds is not null)
        {
            await FyllLokalitetsbuffer(connection, locationIds, cancellationToken);
        }

        klokke.Stop();

        _logger.LogInformation(
            "Publiserte {Observasjoner} observasjoner og {Lokaliteter} lokaliteter "
            + "({Rader} rader i staging) på {Millisekunder} ms",
            batch.Count,
            batch.Locations.Count,
            rader,
            klokke.ElapsedMilliseconds);

        return new PublishResult(batch.Count, batch.Locations.Count, rader, klokke.Elapsed);
    }

    private async Task OpprettStagingTabeller(SqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = string.Join("\n", ImportStagingSchema.All.Select(x => x.CreateTableSql));

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = _innstillinger.CommandTimeoutSeconds
        };

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk-kopierer hver tabell. Tabeller uten rader hoppes over — en tom kopiering
    /// koster en rundtur, og det er tolv av dem per bunt.
    /// </summary>
    private async Task<int> KopierTilStaging(
        SqlConnection connection,
        ImportBatch batch,
        CancellationToken cancellationToken)
    {
        var sum = 0;

        foreach (var tabell in ImportStagingSchema.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var antall = tabell.CountRows(batch);

            if (antall == 0) continue;

            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, externalTransaction: null)
            {
                DestinationTableName = tabell.TempTable,
                BulkCopyTimeout = _innstillinger.BulkCopyTimeoutSeconds,
                BatchSize = 0
            };

            foreach (var kolonne in tabell.Columns)
            {
                bulk.ColumnMappings.Add(kolonne.Name, kolonne.Name);
            }

            using var leser = tabell.CreateReader(batch);

            await bulk.WriteToServerAsync(leser, cancellationToken);

            sum += antall;
        }

        return sum;
    }

    private async Task Kjør(
        SqlConnection connection,
        SqlTransaction transaksjon,
        PublishStep steg,
        DateTime nå,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(steg.Sql, connection, transaksjon)
        {
            CommandTimeout = _innstillinger.CommandTimeoutSeconds
        };

        command.Parameters.Add(PublishSql.NowParameter, SqlDbType.DateTime2).Value = nå;
        command.Parameters.Add(PublishSql.SridParameter, SqlDbType.Int).Value = ImportStagingSchema.Srid;

        try
        {
            var berørte = await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("{Steg}: {Rader} rader", steg.Name, berørte);
        }
        catch (SqlException feil)
        {
            // Uten navnet på steget er en feil fra en runde med tretten setninger
            // vanskelig å plassere.
            throw new InvalidOperationException($"Publiseringssteget {steg.Name} feilet: {feil.Message}", feil);
        }
    }

    private async Task FyllLokalitetsbuffer(
        SqlConnection connection,
        LocationIdCache locationIds,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(PublishSql.SelectLocationIds, connection)
        {
            CommandTimeout = _innstillinger.CommandTimeoutSeconds
        };

        await using var leser = await command.ExecuteReaderAsync(cancellationToken);

        while (await leser.ReadAsync(cancellationToken))
        {
            locationIds.Set(leser.GetString(0), leser.GetInt32(1));
        }
    }
}

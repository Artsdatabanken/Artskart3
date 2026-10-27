using System.Diagnostics;
using Artskart3.Core.Application.Maintenance;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Maintenance;

/// <summary>
/// Innstillinger for etterarbeidet.
/// </summary>
public class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    /// <summary>Tilkoblingen til indeksbasen.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Tidsavbrudd per setning, i sekunder. Tellingene skanner hele
    /// <c>Observation</c>, så de tåler ikke EFs vanlige grense — og en avbrutt telling
    /// etterlater kolonnen nullstilt, ikke halvfylt.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 7200;
}

/// <summary>
/// Kjører tellesetningene, én tabell om gangen.
///
/// RÅ SQL OG EGEN TILKOBLING, ikke EF. Setningene er mengdeoperasjoner over hele
/// basen, det finnes ingen entitet å spore, og de trenger en helt annen tidsgrense enn
/// resten av løsningen. Samme valg som <c>ObservationWriter</c>.
///
/// HVER TABELL FOR SEG, UTEN FELLES TRANSAKSJON. Én transaksjon rundt alle åtte ville
/// holdt låser gjennom hele kjøringen for ingenting: tabellene er uavhengige, og
/// halvveis oppdaterte tellinger er ikke en ugyldig tilstand — de er bare litt gamle,
/// som de er resten av døgnet.
/// </summary>
public class ObservationCountService : IObservationCountService
{
    private readonly MaintenanceOptions _innstillinger;
    private readonly ILogger<ObservationCountService> _logger;

    public ObservationCountService(MaintenanceOptions innstillinger, ILogger<ObservationCountService> logger)
    {
        ArgumentNullException.ThrowIfNull(innstillinger);

        if (string.IsNullOrWhiteSpace(innstillinger.ConnectionString))
        {
            throw new ArgumentException("Etterarbeidet trenger en tilkoblingsstreng.", nameof(innstillinger));
        }

        _innstillinger = innstillinger;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CountResult>> UpdateAllAsync(CancellationToken cancellationToken = default)
    {
        var resultater = new List<CountResult>();

        await using var connection = new SqlConnection(_innstillinger.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var mål in ObservationCountSql.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var klokke = Stopwatch.StartNew();

            try
            {
                await using var command = new SqlCommand(mål.Sql, connection)
                {
                    CommandTimeout = _innstillinger.CommandTimeoutSeconds
                };

                await command.ExecuteNonQueryAsync(cancellationToken);

                klokke.Stop();
                resultater.Add(new CountResult(mål.Table, klokke.Elapsed, null));

                _logger.LogInformation(
                    "{Tabell}: tellinger oppdatert på {Sekunder} s",
                    mål.Table,
                    Math.Round(klokke.Elapsed.TotalSeconds, 1));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception feil)
            {
                klokke.Stop();
                resultater.Add(new CountResult(mål.Table, klokke.Elapsed, feil.Message));

                // Samme valg som Artskart 2: logg og gå videre. Én tabell som feiler
                // skal ikke fryse tallene for de sju andre.
                _logger.LogError(feil, "{Tabell}: tellingene ble ikke oppdatert", mål.Table);
            }
        }

        var feilet = resultater.Count(x => !x.Succeeded);

        _logger.LogInformation(
            "Tellinger ferdig: {Ok} av {Totalt} tabeller oppdatert på {Sekunder} s",
            resultater.Count - feilet,
            resultater.Count,
            Math.Round(resultater.Sum(x => x.Elapsed.TotalSeconds), 1));

        return resultater;
    }
}

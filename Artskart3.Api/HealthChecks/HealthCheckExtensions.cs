using Artskart3.Api.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extensions for configuring health checks in the application.
/// </summary>
public static class HealthCheckExtensions
{
    private const int HealthCheckTimeoutSeconds = 5;
    public static IHealthChecksBuilder AddCustomHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        logger.LogInformation("Configuring Health Checks for Dependencies");

        var dbConnectionString = configuration.GetConnectionString("ArtskartIndex");
        if (!string.IsNullOrEmpty(dbConnectionString))
        {
            logger.LogInformation("Database connection string configured");
            healthChecksBuilder.AddAsyncCheck("Database",
                async () => await CheckDatabaseHealthAsync(dbConnectionString, logger),
                tags: new[] { "dependencies", "database" });

            // Columnstore-indeksen må finnes.
            //
            // Migrasjonene som fyller ObservationEntityIndex SLIPPER den før
            // backfillen og bygger den igjen helt til slutt (se migrasjon
            // 20260920125459). Feiler eller avbrytes migrasjonen i mellom — og
            // gjenoppbyggingen alene tar 10-30 minutter — blir indeksen liggende
            // borte. Program.cs fanger da feilen, logger «application startup will
            // continue», og API-et serverer videre mot en tabell på 134,7 millioner
            // rader uten columnstore. Svarene er riktige, men kategoritellingene går
            // fra ~450 ms til 4-5 s, og én logglinje ved oppstart er hele varselet.
            //
            // DEGRADED, IKKE UNHEALTHY: svarene er korrekte, bare trege, og
            // Unhealthy gir 503 på /hc. Er /hc en readiness-probe, ville en halvveis
            // anvendt migrasjon tatt appen ut av rotasjon eller startet en
            // omstartsløkke — og en omstart midt i migrasjonen er nettopp det som
            // etterlater indeksen borte. Degraded gir 200 med status i svaret:
            // synlig for overvåking, uten å gjøre vondt verre.
            healthChecksBuilder.AddAsyncCheck("Columnstore",
                async () => await CheckColumnstoreHealthAsync(dbConnectionString, logger),
                tags: new[] { "dependencies", "database", "performance" });
        }
        else
        {
            logger.LogWarning("Database connection string NOT configured");
        }

        var keyVaultUrl = configuration["KeyVault:Url"];
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            logger.LogInformation("Key Vault configured at: {KeyVaultUrl}", keyVaultUrl);
            healthChecksBuilder.AddAsyncCheck("Key Vault",
                async () => await CheckKeyVaultHealthAsync(keyVaultUrl, logger),
                tags: new[] { "dependencies", "security" });
        }
        else
        {
            logger.LogDebug("Key Vault NOT configured (optional)");
        }

        logger.LogInformation("Health checks configuration completed");
        return healthChecksBuilder;
    }
    private static async Task<HealthCheckResult> ExecuteHealthCheckAsync(
     string serviceName,
     Func<CancellationToken, Task> healthCheckFunc,
     ILogger logger)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));
            await healthCheckFunc(cts.Token);
            logger.LogDebug("{ServiceName} health check passed", serviceName);
            return HealthCheckResult.Healthy($"{serviceName} is responsive");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("{ServiceName} health check timed out after {Timeout} seconds", serviceName, HealthCheckTimeoutSeconds);
            return HealthCheckResult.Unhealthy($"{serviceName} health check timeout");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ServiceName} health check failed: {Message}", serviceName, ex.Message);
            return HealthCheckResult.Unhealthy($"{serviceName} connection failed: {ex.Message}");
        }
    }

    private static async Task<HealthCheckResult> CheckDatabaseHealthAsync(string connectionString, ILogger logger)
    {
        return await ExecuteHealthCheckAsync("Database", async (ct) =>
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = HealthCheckTimeoutSeconds;
            await command.ExecuteScalarAsync(ct);
        }, logger);
    }

    /// <summary>
    /// Columnstore-indeksen på dbo.ObservationEntityIndex. Se begrunnelsen der
    /// sjekken registreres for hvorfor et manglende columnstore er «degraded» og
    /// ikke «unhealthy».
    ///
    /// Bruker ikke <see cref="ExecuteHealthCheckAsync"/>: den kjenner bare to utfall
    /// — ok eller unhealthy — og kan ikke uttrykke det tredje, som er hele poenget
    /// her. Tidsavbruddet og unntakshåndteringen er ellers de samme.
    /// </summary>
    private static async Task<HealthCheckResult> CheckColumnstoreHealthAsync(string connectionString, ILogger logger)
    {
        const string indeks = "IX_OEI_Columnstore";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cts.Token);

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sys.indexes " +
                "WHERE name = @navn AND object_id = OBJECT_ID('dbo.ObservationEntityIndex');";
            command.Parameters.AddWithValue("@navn", indeks);
            command.CommandTimeout = HealthCheckTimeoutSeconds;

            var funnet = Convert.ToInt32(await command.ExecuteScalarAsync(cts.Token)) > 0;

            if (funnet)
            {
                logger.LogDebug("Columnstore health check passed");
                return HealthCheckResult.Healthy($"{indeks} finnes");
            }

            // LogError og ikke LogWarning: dette er en tilstand noen må rydde opp i,
            // og den blir ikke bedre av seg selv.
            logger.LogError(
                "{Indeks} mangler på dbo.ObservationEntityIndex. En migrasjon er sannsynligvis halvveis anvendt. Spørringene svarer riktig, men betydelig tregere.",
                indeks);

            return HealthCheckResult.Degraded(
                $"{indeks} mangler. Trolig en halvveis anvendt migrasjon — svarene er riktige, men tregere.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Columnstore health check timed out after {Timeout} seconds", HealthCheckTimeoutSeconds);
            return HealthCheckResult.Degraded($"{indeks} kunne ikke verifiseres: tidsavbrudd");
        }
        catch (Exception ex)
        {
            // Databasesjekken over eier «databasen svarer ikke». Her betyr et unntak
            // bare at tilstanden er ukjent, og ukjent er ikke det samme som borte.
            logger.LogWarning(ex, "Columnstore health check failed: {Message}", ex.Message);
            return HealthCheckResult.Degraded($"{indeks} kunne ikke verifiseres: {ex.Message}");
        }
    }

    private static async Task<HealthCheckResult> CheckKeyVaultHealthAsync(string keyVaultUrl, ILogger logger)
    {
        return await ExecuteHealthCheckAsync("Key Vault", async (ct) =>
        {
            if (string.IsNullOrWhiteSpace(keyVaultUrl) || !Uri.TryCreate(keyVaultUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException($"Invalid Key Vault URL: {keyVaultUrl}");

            await Task.CompletedTask;
        }, logger);
    }
    public static async Task WriteJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration,
            Entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new HealthCheckEntry
                {
                    Data = new Dictionary<string, object>(kvp.Value.Data),
                    Duration = kvp.Value.Duration,
                    Status = kvp.Value.Status.ToString(),
                    Tags = kvp.Value.Tags.ToList(),
                    Description = kvp.Value.Description
                }
            )
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}

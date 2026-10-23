using Artskart3.Api.HealthChecks;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Persistence.Services;
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

            // Tellebufferne må være Ready, ellers aggregerer hvert område- og
            // lokasjonskall live.
            //
            // Det er den NORMALE tilstanden etter en deploy: 20261021125247 tømmer
            // begge bufferne og setter Status = 'Building', og det er
            // RebuildCountCaches som fyller dem igjen. Avbrytes den — eller kjøres
            // skriptene manuelt i feil rekkefølge — står bufferne tomme.
            //
            // Og den tilstanden var HELT stille: oppslaget returnerer null uten en
            // logglinje når Status ikke er 'Ready' (se KlarTilstandAsync i begge
            // tjenestene). Skjemaversjon og SourceRows logger en advarsel, men
            // 'Building' gjør det ikke. Målt kostnad: 2119 ms mot 84 ms for det
            // ufiltrerte lokasjonsoppslaget.
            healthChecksBuilder.AddAsyncCheck("CountCaches",
                async () => await CheckCountCachesHealthAsync(dbConnectionString, logger),
                tags: new[] { "dependencies", "database", "performance" });
        }
        else
        {
            logger.LogWarning("Database connection string NOT configured");
        }

        // Polygondatasettet i minnet. Registreres BARE når lageret finnes i
        // containeren: Workers kaller ikke AddApplicationServices, og testene som
        // kaller AddCustomHealthChecks på en naken ServiceCollection skal ikke få en
        // sjekk som kaster når den kjøres. Betingelsen leses av registreringene,
        // ikke av tjenestetilbyderen, så den er avgjort før appen starter.
        if (services.Any(d => d.ServiceType == typeof(IPolygonLocationStore)))
        {
            healthChecksBuilder.AddCheck<PolygonStoreHealthCheck>(
                "PolygonStore",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "dependencies", "performance" });
        }
        else
        {
            logger.LogDebug("PolygonLocationStore not registered — health check skipped");
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

            // Samme grunn som i CheckColumnstoreHealthAsync: Description serveres
            // anonymt fra /hc, og en tilkoblingsfeil navngir server og database.
            // Typenavnet triagerer; detaljene står i loggen.
            return HealthCheckResult.Unhealthy($"{serviceName} connection failed: {ex.GetType().Name}, see logs");
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

            // UNNTAKSTEKSTEN HØRER I LOGGEN, IKKE I SVARET
            //
            // Description havner i JSON-kroppen (se WriteJsonResponse), og /hc er
            // kartlagt uten RequireAuthorization — altså anonymt. En SqlException
            // navngir rutinemessig server, instans, database og pålogging:
            // «Cannot open database "..." requested by the login. The login failed
            // for user "..."». Det er kartlegging av infrastrukturen vår, gratis.
            //
            // Typenavnet er nok for å triagere og lekker ingenting. Hele unntaket
            // med stakk står i logglinjen over.
            return HealthCheckResult.Degraded($"{indeks} kunne ikke verifiseres: {ex.GetType().Name}, se loggen");
        }
    }

    /// <summary>
    /// Tilstanden til de to tellebufferne, lest fra tilstandsradene deres.
    ///
    /// Tre ting kan gjøre en buffer ubrukelig for oppslaget, og sjekken rapporterer
    /// hvilken — ellers må den som feilsøker gjette:
    ///
    ///   Status er ikke 'Ready'   tømt eller halvbygget. Normal tilstand rett etter
    ///                            en deploy, og den eneste av de tre som IKKE
    ///                            logger noe fra oppslaget.
    ///   SchemaVersion avviker    dimensjonsregisteret eller radformen er endret, så
    ///                            bøtte-id-ene i tabellen betyr ikke det koden tror
    ///   SourceRows avviker       ObservationEntityIndex har flyttet seg siden
    ///                            bufferen ble bygget — en import eller et skript
    ///
    /// Radantallet leses med <see cref="CountCacheSourceRows.Sql"/>, samme uttrykk
    /// oppslaget bruker. Det MÅ være samme uttrykk: to tall målt på hver sin måte
    /// ville gjort sjekken uenig med tjenesten den rapporterer om.
    ///
    /// DEGRADED, IKKE UNHEALTHY — samme begrunnelse som for Columnstore: svarene er
    /// riktige, bare trege, og 503 på /hc hjelper ingen.
    ///
    /// Finnes ikke tabellene ennå (en database som ligger foran denne grenen),
    /// havner vi i catch og rapporterer Degraded med unntakstypen. Det er riktig:
    /// vi vet da ikke om bufferne er i orden.
    /// </summary>
    private static async Task<HealthCheckResult> CheckCountCachesHealthAsync(string connectionString, ILogger logger)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cts.Token);

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT 'omraade' AS Buffer, Status, SchemaVersion, SourceRows
                FROM dbo.AreaCountCacheState
                UNION ALL
                SELECT 'lokasjon', Status, SchemaVersion, SourceRows
                FROM dbo.LocationCountCacheState WHERE Id = 1;

                {CountCacheSourceRows.Sql};
                """;
            command.CommandTimeout = HealthCheckTimeoutSeconds;

            var avvik = new List<string>();
            var kilderaderPerBuffer = new Dictionary<string, long>(StringComparer.Ordinal);
            var antallBuffere = 0;

            using var reader = await command.ExecuteReaderAsync(cts.Token);
            while (await reader.ReadAsync(cts.Token))
            {
                antallBuffere++;
                var buffer = reader.GetString(0);
                var status = reader.GetString(1);
                var skjemaversjon = reader.GetInt32(2);

                if (!string.Equals(status, "Ready", StringComparison.Ordinal))
                    avvik.Add($"{buffer}: Status={status}");
                else if (skjemaversjon != AreaCountCacheDimensions.SchemaVersion)
                    avvik.Add($"{buffer}: bygget med skjemaversjon {skjemaversjon}, koden forventer {AreaCountCacheDimensions.SchemaVersion}");
                else if (await reader.IsDBNullAsync(3, cts.Token))
                    avvik.Add($"{buffer}: SourceRows mangler, kan ikke verifiseres mot dataene");
                else
                    kilderaderPerBuffer[buffer] = reader.GetInt64(3);
            }

            // Andre resultatsett: radantallet slik det er NÅ.
            long? naa = null;
            if (await reader.NextResultAsync(cts.Token) && await reader.ReadAsync(cts.Token))
                naa = reader.GetInt64(0);

            foreach (var (buffer, bygget) in kilderaderPerBuffer)
            {
                if (naa is { } n && bygget != n)
                    avvik.Add($"{buffer}: bygget fra {bygget:N0} rader, tabellen har {n:N0}");
            }

            if (antallBuffere < 2)
                avvik.Add($"fant bare {antallBuffere} av 2 tilstandsrader");

            if (avvik.Count == 0)
            {
                logger.LogDebug("Count cache health check passed");
                return HealthCheckResult.Healthy("Begge tellebufferne er klare og ferske");
            }

            // LogWarning og ikke LogError: dette er en tilstand som oppstår ved hver
            // deploy og rettes av at noen bygger bufferne. Den er ikke et havari.
            logger.LogWarning(
                "Tellebufferne er ikke i bruk: {Avvik}. Spørringene svarer riktig, men aggregerer live (målt 2119 ms mot 84 ms ufiltrert). Kjør Scripts/BuildAreaCountCache.sql og deretter BuildLocationCountCache.sql.",
                string.Join("; ", avvik));

            return HealthCheckResult.Degraded(
                $"Tellebufferne er ikke i bruk: {string.Join("; ", avvik)}. " +
                "Kjør BuildAreaCountCache.sql og deretter BuildLocationCountCache.sql.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Count cache health check timed out after {Timeout} seconds", HealthCheckTimeoutSeconds);
            return HealthCheckResult.Degraded("Tellebufferne kunne ikke verifiseres: tidsavbrudd");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Count cache health check failed: {Message}", ex.Message);

            // Samme grunn som ellers i denne filen: unntaksteksten serveres anonymt.
            return HealthCheckResult.Degraded($"Tellebufferne kunne ikke verifiseres: {ex.GetType().Name}, se loggen");
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

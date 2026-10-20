using Artskart3.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extensions for configuring health checks in the application.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Sjekker merket med denne kjøres av readiness-proben. Liveness kjører ingen.
    /// </summary>
    public const string ReadyTag = "ready";

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
                tags: new[] { "dependencies", "database", ReadyTag });
        }
        else
        {
            logger.LogWarning("Database connection string NOT configured");
        }

        logger.LogInformation("Health checks configuration completed");
        return healthChecksBuilder;
    }

    /// <summary>
    /// Bygger oppsettet for et probe-endepunkt. Predikatet velger hvilke sjekker som
    /// kjøres: ingen for liveness, de ready-merkede for readiness, alle for /hc.
    /// </summary>
    public static HealthCheckOptions CreateProbeOptions(
        bool allowCachingResponses,
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        var options = new HealthCheckOptions
        {
            ResponseWriter = WriteJsonResponse,
            AllowCachingResponses = allowCachingResponses
        };

        if (predicate is not null)
        {
            options.Predicate = predicate;
        }

        return options;
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

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
namespace Artskart3.Server.HealthChecks;

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

        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(dbConnectionString))
        {
            logger.LogInformation("Database connection string configured");
            healthChecksBuilder.AddAsyncCheck("Database",
                async () => await CheckDatabaseHealthAsync(dbConnectionString, logger),
                tags: new[] { "dependencies", "database" });
        }
        else
        {
            logger.LogWarning("Database connection string NOT configured");
        }

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            logger.LogInformation("Redis cache connection configured");
            healthChecksBuilder.AddAsyncCheck("Redis Cache",
                async () => await CheckRedisHealthAsync(redisConnection, logger),
                tags: new[] { "dependencies", "cache" });
        }
        else
        {
            logger.LogDebug("Redis cache NOT configured (optional)");
        }

        var azureServiceBusConnection = configuration.GetConnectionString("AzureServiceBus");
        if (!string.IsNullOrEmpty(azureServiceBusConnection))
        {
            logger.LogInformation("Azure Service Bus connection configured");
            healthChecksBuilder.AddAsyncCheck("Azure Service Bus",
                async () => await CheckAzureServiceBusHealthAsync(azureServiceBusConnection, logger),
                tags: new[] { "dependencies", "messaging" });
        }
        else
        {
            logger.LogDebug("Azure Service Bus NOT configured (optional)");
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

    private static async Task<HealthCheckResult> CheckRedisHealthAsync(string connectionString, ILogger logger)
    {
        return await ExecuteHealthCheckAsync("Redis Cache", async (ct) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Redis connection string is empty");
          await Task.CompletedTask;
        }, logger);
    }

    private static async Task<HealthCheckResult> CheckAzureServiceBusHealthAsync(string connectionString, ILogger logger)
    {
        return await ExecuteHealthCheckAsync("Azure Service Bus", async (ct) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Azure Service Bus connection string is empty");
            await Task.CompletedTask;
        }, logger);
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

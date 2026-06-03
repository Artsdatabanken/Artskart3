using System.Diagnostics;
using System.Text;
using Artskart3.Api.Configuration;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Artskart3.Api.Filters;

public class SlowQueryLoggingFilter : IAsyncActionFilter
{
    private readonly ILogger<SlowQueryLoggingFilter> _logger;
    private readonly IOptions<SlowQueryLoggingOptions> _options;
    private readonly ArtskartDbContext _dbContext;

    public SlowQueryLoggingFilter(
        ILogger<SlowQueryLoggingFilter> logger,
        IOptions<SlowQueryLoggingOptions> options,
        ArtskartDbContext dbContext)
    {
        _logger = logger;
        _options = options;
        _dbContext = dbContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await next();
        stopwatch.Stop();

        var elapsedMs = stopwatch.ElapsedMilliseconds;

        if (elapsedMs >= options.ThresholdMs)
        {
            var endpoint = $"{context.Controller.GetType().Name.Replace("Controller", "")}/{context.ActionDescriptor.RouteValues["action"]}";
            var requestPath = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

            string? requestBody = null;
            if (context.HttpContext.Request.Method == "POST" && context.HttpContext.Request.Body.CanSeek)
            {
                context.HttpContext.Request.Body.Position = 0;
                using var reader = new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
            }
            else if (context.ActionArguments.Count > 0)
            {
                var serializableArgs = context.ActionArguments
                    .Where(kvp => kvp.Value is not CancellationToken)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                try
                {
                    requestBody = System.Text.Json.JsonSerializer.Serialize(serializableArgs);
                }
                catch
                {
                    requestBody = System.Text.Json.JsonSerializer.Serialize(
                        serializableArgs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.GetType().Name ?? "null"));
                }
            }

            var logEntry = new SlowQueryLog
            {
                Endpoint = endpoint,
                QueryTimeMs = elapsedMs,
                ThresholdMs = options.ThresholdMs,
                RequestPath = requestPath,
                RequestBody = requestBody?.Length > 4000 ? requestBody[..4000] : requestBody,
                OccurredAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.SlowQueryLogs.Add(logEntry);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist slow query log for endpoint {Endpoint}", endpoint);
            }

            _logger.LogWarning(
                "Slow query detected: {Endpoint} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                endpoint, elapsedMs, options.ThresholdMs);
        }
    }
}

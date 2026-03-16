using RobotsTxt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStaticRobotsTxt(options =>
{
    if (!builder.Environment.IsProduction())
        options.DenyAll();
    else
        options.AddSection(section => section
            .AddUserAgent("*")
            .Allow("/")
            .AddCrawlDelay(TimeSpan.FromSeconds(1)));

    return options;
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.Extensions.Diagnostics", LogLevel.Information);
}

var tempLoggerFactory = LoggerFactory.Create(c => c.AddConsole().AddDebug());
var logger = tempLoggerFactory.CreateLogger("Startup");

try
{
    logger.LogInformation("ArtsKart3 Server - Application Startup");
    logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
    logger.LogInformation("Machine: {MachineName}", Environment.MachineName);
    logger.LogInformation("Building services...");

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddApplicationInsightsTelemetry(new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions
    {
        ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    });
    
    logger.LogInformation("Configuring health checks...");
    builder.Services.AddCustomHealthChecks(builder.Configuration, logger);
    
    logger.LogInformation("Services configured successfully");

    var app = builder.Build();

    AddRobotsConfiguration(builder.Configuration, app);

    logger.LogInformation("Building application pipeline...");

    app.UseDefaultFiles();
    app.MapStaticAssets();

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("Development environment detected - enabling OpenAPI");
        app.MapOpenApi();
    }
    else
    {
        logger.LogInformation("Production environment - disabling detailed API documentation");
    }

    var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckExtensions.WriteJsonResponse,
        AllowCachingResponses = !app.Environment.IsDevelopment()
    };
    
    app.MapHealthChecks("/hc", healthCheckOptions);
    logger.LogInformation("Health check endpoint mapped to '/hc'");

    app.UseAuthorization();
    app.MapControllers();

    app.MapFallbackToFile("/index.html");

    logger.LogInformation("Application Started Successfully");
   
    app.Run();
}
catch (Exception ex)
{
    logger.LogCritical("Application terminated unexpectedly with exception:");
    logger.LogCritical("Message: {Message}", ex.Message);
    logger.LogCritical("StackTrace: {StackTrace}", ex.StackTrace);
    throw;
}

return;

void AddRobotsConfiguration(ConfigurationManager configuration, WebApplication webApplication)
{
    // startup config
    var allowRobotsInProduction = Convert.ToBoolean(configuration["Application:AllowRobotsInProduction"]);

    // Configure the HTTP request pipeline.
    webApplication.Use(async (context, next) =>
    {
        if (context.Request.Path == "/robots.txt")
        {
            context.Response.ContentType = "text/plain";

            if (webApplication.Environment.IsDevelopment() || webApplication.Environment.IsEnvironment("Test") || !allowRobotsInProduction)
            {
                await context.Response.WriteAsync("User-agent: *\nDisallow: /\n");
            }
            else
            {
                await context.Response.WriteAsync("User-agent: *\nAllow: /\nCrawl-delay: 1\n");
            }

            return;
        }

        await next();
    });
}

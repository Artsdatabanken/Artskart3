
using RobotsTxt;
using Microsoft.EntityFrameworkCore;
using Artskart3.Infrastructure.DependencyInjection;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Data;

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

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    });
    builder.Services.AddSwaggerGen();
    builder.Services.AddApplicationInsightsTelemetry(new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions
    {
        ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    });

    var dbConnectionString = builder.Configuration.GetConnectionString("ArtskartDb");   
    builder.Services.AddDbContext<ArtskartDbContext>(options =>
        options.UseSqlServer(dbConnectionString));

    builder.Services.AddRepositories();
    builder.Services.AddApplicationServices();
    builder.Services.AddScoped<IArtsKartDbContext>(provider => provider.GetRequiredService<ArtskartDbContext>());

    logger.LogInformation("Configuring health checks...");
    builder.Services.AddCustomHealthChecks(builder.Configuration, logger);
    logger.LogInformation("Services configured successfully");

    var app = builder.Build();
    
    // Configure the HTTP request pipeline.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/robots.txt")
        {
            context.Response.ContentType = "text/plain";

            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
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
    
    logger.LogInformation("Building application pipeline...");

    app.UseDefaultFiles();
    app.MapStaticAssets();

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("Development environment detected - enabling Swagger UI");
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ArtsKart3 API v1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
        });
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

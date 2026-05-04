
using Artskart3.Api.Middleware;
using RobotsTxt;
using Microsoft.EntityFrameworkCore;
using Artskart3.Infrastructure.DependencyInjection;
using Artskart3.Infrastructure.Persistence.Repositories;
using Artskart3.Infrastructure.Data;
using Duende.Bff;
using Duende.Bff.EntityFramework;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStaticRobotsTxt(options =>
{
    // TODO: Allow crawlers in Production after launch
    // For now, block all crawlers to prevent indexing before official launch
    options.DenyAll();
    return options;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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

    // Configure CORS for local development - allows frontend to call backend API
    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // For development: allow any origin, method, and header
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        }
        else
        {
            // For production: restrict to specific origins from configuration
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            if (allowedOrigins != null && allowedOrigins.Length > 0)
            {
                logger.LogInformation("Configuring CORS for production with {OriginCount} allowed origins", allowedOrigins.Length);
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            }
            else
            {
                logger.LogWarning("CORS AllowedOrigins not configured in production - denying all cross-origin requests");
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins();
                });
            }
        }
    });

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    });
    builder.Services.AddSwaggerGen();
    
    var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        logger.LogInformation("ApplicationInsights telemetry enabled");
        builder.Services.AddApplicationInsightsTelemetry(new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions
        {
            ConnectionString = appInsightsConnectionString
        });
    }
    else
    {
        logger.LogWarning("ApplicationInsights connection string not configured - telemetry will not be collected");
    }

    var dbConnectionString = builder.Configuration.GetConnectionString("ArtskartDb");   
    builder.Services.AddBff()
        .ConfigureOpenIdConnect(options =>
        {
            options.Authority = "https://demo.duendesoftware.com";
            options.ClientId = "interactive.confidential";
            options.ClientSecret = "secret";
            options.ResponseType = "code";
            options.ResponseMode = "query";
        
            options.GetClaimsFromUserInfoEndpoint = true;
            options.SaveTokens = true;
            options.MapInboundClaims = false;
        
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("offline_access");
        }).ConfigureCookies(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
        })
        .AddServerSideSessions()
        .AddEntityFrameworkServerSideSessions(options =>
        {
            options.UseSqlServer(dbConnectionString);
        });

    builder.Services.AddAuthorization();
    builder.Services.AddDbContext<ArtskartDbContext>(options =>
    {
        options.UseSqlServer(dbConnectionString, x => x.UseNetTopologySuite());
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddRepositories();
    builder.Services.AddApplicationServices();
    builder.Services.AddScoped<IArtsKartDbContext>(provider => provider.GetRequiredService<ArtskartDbContext>());

    logger.LogInformation("Configuring health checks...");
    builder.Services.AddCustomHealthChecks(builder.Configuration, logger);
    logger.LogInformation("Services configured successfully");

    var app = builder.Build();

    // Auto-apply pending migrations only if enabled in configuration
    var autoMigrateDb = Convert.ToBoolean(builder.Configuration["Database:AutoMigrate"] ?? "false");
    if (autoMigrateDb && !builder.Environment.IsDevelopment())
    {
        logger.LogWarning("AutoMigrate is enabled in non-development environment - this is not recommended for production");
    }
    
    if (autoMigrateDb)
    {
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
                logger.LogInformation("Applying pending database migrations...");
                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations - application startup will continue");
        }
    }
    else
    {
        logger.LogInformation("Automatic database migrations disabled (Database:AutoMigrate=false)");
    }

    app.UseForwardedHeaders();
    AddRobotsConfiguration(builder.Configuration, app);

    logger.LogInformation("Building application pipeline...");

    // CORS must be early in the pipeline - before controllers and static files
    app.UseCors();
    
    app.UseDefaultFiles();
    app.MapStaticAssets();
    app.UseMiddleware<ClientSafeListMiddleware>(builder.Configuration["ClientSafeList"]);

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
        logger.LogInformation("Production environment - Swagger UI disabled for security");
    }

    var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckExtensions.WriteJsonResponse,
        AllowCachingResponses = !app.Environment.IsDevelopment()
    };
    
    app.MapHealthChecks("/hc", healthCheckOptions);
    logger.LogInformation("Health check endpoint mapped to '/hc'");
    
    app.UseBff();
    app.UseAuthorization();
    app.MapControllers()
        .RequireAuthorization()
        .AsBffApiEndpoint();

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

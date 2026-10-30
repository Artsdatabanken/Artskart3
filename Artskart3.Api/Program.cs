using System.Threading.RateLimiting;
using Artskart3.Api.Configuration;
using Artskart3.Api.Filters;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Constants;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.DependencyInjection;
using Azure.Identity;
using Duende.Bff;
using Duende.Bff.EntityFramework;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

// Stiene responskomprimering slås på for. Svarene her er store, svært
// komprimerbare og offentlige: observasjonsdata, områdegeometri og kartlag — ingen
// tokens, ingen sesjonsdata. Det er forutsetningen for EnableForHttps, så listen
// skal holdes kort og hvert tillegg vurderes mot den. Alt utenfor, /bff/*
// inkludert, komprimeres ikke.
string[] CompressedPaths = ["/api/Search", "/api/observations", "/api/MapLayer", "/api/Lookup"];

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.Extensions.Diagnostics", LogLevel.Information);

    var keyVaultUrl = new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUrl, new DefaultAzureCredential());
}

var tempLoggerFactory = LoggerFactory.Create(c => c.AddConsole().AddDebug());
var logger = tempLoggerFactory.CreateLogger("Startup");

try
{
    logger.LogInformation("ArtsKart3 Server - Application Startup");
    logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
    logger.LogInformation("Machine: {MachineName}", Environment.MachineName);
    logger.LogInformation("Building services...");

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardedHostHeaderName = "X-Original-Host";
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

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

    // SizeLimit, i byte. Uten en grense vokser bufferen fritt og kan ikke kaste noe
    // ut under minnepress: zoomnivå 1 og 2 holder områdegeometrien som DTO-er (~15 MB
    // WKT), som rå UTF-8 (20,7 MB) og som gzip (7,3 MB), og områdetellingene nøkles
    // på en filterhash og har dermed et ubegrenset nøkkelrom. Grensen gjør at de
    // eldste postene forsvinner framfor at prosessen vokser.
    //
    // MERK: med SizeLimit satt MÅ hver Set oppgi Size, ellers kaster den. Alle Set-ene
    // på denne bufferen ligger i SearchService og CadastralUnitService.
    builder.Services.AddMemoryCache(options => options.SizeLimit = 128 * 1024 * 1024);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Håndtereren er FELLES for alle policyene, så den må si hvilken som avviste.
        // Sto meldingen fast på kartkøen, ville et avvist søk rapportert seg som et
        // kartproblem — i svaret, og i loggen noen leser mens de feilsøker.
        options.OnRejected = async (context, cancellationToken) =>
        {
            var sti = context.HttpContext.Request.Path.Value ?? "ukjent";
            var erSoek = sti.StartsWith("/api/Search", StringComparison.OrdinalIgnoreCase);

            if (erSoek)
            {
                logger.LogWarning("Search request queue is full ({Path})", sti);
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Too many search requests. Please retry." }, cancellationToken);
            }
            else
            {
                logger.LogWarning("Observation map request queue is full ({Path})", sti);
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Too many map requests. Please retry." }, cancellationToken);
            }
        };
        options.AddConcurrencyLimiter("observation-maps", limiter =>
        {
            limiter.PermitLimit = 4;
            limiter.QueueLimit = 16;
            limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        });

        // Søkeendepunktene er anonyme, og de dyre av dem leser titalls millioner
        // indeksrader. De har tak hver for seg — sidetall, filterlengder, antall
        // grener — men ingenting begrenset hvor MANGE slike kall som kunne være i
        // gang samtidig. Dette taket beskytter tilkoblingspoolen og databasen.
        //
        // GLOBALT, IKKE PER IP — OG DET ER IKKE FØRSTEVALGET
        // Et tak per klient ville vært bedre: det rammer den som misbruker framfor
        // alle. Men klient-IP-en er ikke til å stole på her. ForwardedHeaders er
        // konfigurert med TOMME KnownIPNetworks og KnownProxies (se over), og
        // ForwardedHeadersMiddleware validerer bare når minst én av dem har
        // innhold — altså ikke i det hele tatt. RemoteIpAddress er da det kalleren
        // selv skrev i X-Forwarded-For. En partisjonsnøkkel derfra gir to problemer:
        // taket omgås ved å variere headeren, og hver nye verdi lager en ny
        // partisjon, så «vernet» blir i seg selv en minnelekkasje under angrep.
        //
        // Ett globalt tak kan ikke omgås og bruker én limiter. Prisen er at en
        // misbrukende klient kan fylle køen for andre — en dårligere egenskap enn
        // et IP-tak ville hatt, men bedre enn et tak som ikke virker.
        //
        // SKAL DET BLI PER IP: fyll KnownIPNetworks/KnownProxies med de faktiske
        // ingress-adressene og sett ForwardLimit. Da, og først da, er
        // RemoteIpAddress klientens og ikke klientens påstand.
        options.AddPolicy(SearchConstants.SearchRateLimitPolicy, _ =>
            RateLimitPartition.GetConcurrencyLimiter(
                "search",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 32,
                    QueueLimit = 64,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                }));
    });
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<GlobalExceptionFilter>();
    }).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
    builder.Services.AddSwaggerGen(c =>
    {
        c.SupportNonNullableReferenceTypes();
        c.OperationFilter<Artskart3.Api.Filters.ExportRequestExampleFilter>();
    });

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

    var dbConnectionString = builder.Configuration.GetConnectionString("ArtskartIndex");
    builder.Services.AddBff()
        .ConfigureOpenIdConnect(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.ClientId = builder.Configuration["Auth:ClientId"];
            options.ClientSecret = builder.Configuration["Auth:ClientSecret"];
            options.ResponseType = "code";
            options.ResponseMode = "query";

            options.GetClaimsFromUserInfoEndpoint = true;
            options.SaveTokens = true;
            options.MapInboundClaims = false;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.Scope.Add("profile");
            options.Scope.Add("roles");

            options.Events.OnTicketReceived = async context =>
            {
                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var subject = context.Principal?.FindFirst("sub")?.Value;
                if (!Guid.TryParse(subject, out var userId))
                {
                    context.Fail("Authenticated user is missing");
                    return;
                }

                var user = new User
                {
                    Id = userId,
                    Name = context.Principal?.FindFirst("name")?.Value ?? string.Empty,
                    Email = context.Principal?.FindFirst("email")?.Value ?? string.Empty,
                };
                await userService.GetOrCreateUser(user);
            };

        }).ConfigureCookies(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
        })
        .AddServerSideSessions()
        .AddEntityFrameworkServerSideSessions(options =>
        {
            options.UseSqlServer(dbConnectionString, sqlOptions => sqlOptions.MigrationsAssembly("Artskart3.Infrastructure"));
        })
        .AddSessionCleanupBackgroundProcess();

    builder.Services.AddAuthorization();
    builder.Services.AddDbContext<ArtskartDbContext>(options =>
    {
        options.UseSqlServer(dbConnectionString, x =>
        {
            x.UseNetTopologySuite();
            x.CommandTimeout(1800);
        });
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        // Legger OPTION (RECOMPILE) på listevisningens spørring. Se
        // QueryHintInterceptor for hvorfor den trenger plan per kjøring.
        options.AddInterceptors(new Artskart3.Infrastructure.Data.Interceptors.QueryHintInterceptor());
    });

    builder.Services.AddRepositories();
    builder.Services.AddApplicationServices();

    var norTaxaSection = builder.Configuration.GetSection(NorTaxaOptions.SectionName);
    builder.Services.Configure<NorTaxaOptions>(norTaxaSection);
    var norTaxaOptions = norTaxaSection.Get<NorTaxaOptions>() ?? new NorTaxaOptions();

    builder.Services.AddHttpClient<ISpeciesService, SpeciesService>(client =>
    {
        client.BaseAddress = new Uri(norTaxaOptions.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(norTaxaOptions.TimeoutSeconds);
    });
    builder.Services.AddCadastralUnitService(
        builder.Configuration.GetSection(KartverketEiendomOptions.SectionName).Get<KartverketEiendomOptions>() ?? new KartverketEiendomOptions());
    builder.Services.AddScoped<IArtsKartDbContext>(provider => provider.GetRequiredService<ArtskartDbContext>());

    builder.Services.Configure<SlowQueryLoggingOptions>(builder.Configuration.GetSection(SlowQueryLoggingOptions.SectionName));
    builder.Services.Configure<PaginationOptions>(builder.Configuration.GetSection(PaginationOptions.SectionName));
    builder.Services.Configure<AreaCountCacheOptions>(builder.Configuration.GetSection(AreaCountCacheOptions.SectionName));
    builder.Services.Configure<LocationCountCacheOptions>(builder.Configuration.GetSection(LocationCountCacheOptions.SectionName));

    // Responskomprimering. Svarene er store og svært komprimerbare: AreaMarkers på
    // zoomnivå 2 er 20,7 MB rå og 7,26 MB gzippet — 65 % mindre. Zoomnivå 1 går fra
    // 3,9 til 1,38 MB. GeoJSON og WKT er tekst med mye repetisjon.
    //
    // EnableForHttps: BREACH/CRIME utnytter at komprimering lekker informasjon når et
    // svar blander en hemmelighet med noe angriperen styrer. Endepunktene dette gjelder
    // returnerer observasjonsdata og geometri — ingen tokens, ingen sesjonsdata — så
    // det gjelder ikke der.
    //
    // DET ER OGSÅ HVORFOR MIDDLEWAREN ER AVGRENSET, ikke global. Begrunnelsen over
    // gjelder noen endepunkter, mens app.UseResponseCompression() gjelder alle — også
    // /bff/user, som returnerer den innloggede brukerens claims som JSON. Kjøres den
    // for alt, arver neste endepunkt unntaket i stillhet. Se CompressedPaths nedenfor.
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/geo+json"]);
    });
    builder.Services.AddScoped<SlowQueryLoggingFilter>();

    logger.LogInformation("Configuring health checks...");
    builder.Services.AddCustomHealthChecks(builder.Configuration, logger);
    logger.LogInformation("Services configured successfully");

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Tidlig i pipelinen, foer noe skriver til svaret. Hopper over svar som allerede
    // har Content-Encoding satt (se AreaMarkers, som serverer ferdig gzippede bytes).
    //
    // Avgrenset til stiene komprimeringen er begrunnet for — store, offentlige
    // geometri- og observasjonssvar. Alt annet, /bff/* inkludert, komprimeres ikke:
    // begrunnelsen for EnableForHttps hviler på at svaret ikke blander en hemmelighet
    // med noe en angriper styrer, og det kan vi ikke love for endepunkter som ennå
    // ikke er skrevet.
    app.UseWhen(
        ctx => CompressedPaths.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)),
        branch => branch.UseResponseCompression());

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

                // Migrasjoner får sin egen kommandotimeout — 6 timer.
                //
                // CommandTimeout(1800) fra DbContext-registreringen er riktig for
                // spørringer som betjener forespørsler, men altfor kort for
                // datafyllende migrasjoner. BackfillAll kjører hele datasettet som
                // én kommando, og hele løkken teller mot ett budsjett.
                //
                // SetCommandTimeout gjelder bare denne ene instansen, hentet fra et
                // eget scope. Kontekstene som betjener HTTP-forespørsler beholder
                // sine 30 minutter — en løpsk spørring henger fortsatt ikke i timevis.
                //
                // MERK: Ved 6 timers migrasjon kommer ikke appen opp før den er
                // ferdig. På plattformer med helsesjekk-timeout kan containeren bli
                // drept underveis. Deployede miljøer bør kjøre migrasjonsbundtet fra
                // pipelinen og sette Database:AutoMigrate = false.
                dbContext.Database.SetCommandTimeout((int)TimeSpan.FromHours(6).TotalSeconds);
                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully");
                var sessionDbContext = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
                logger.LogInformation("Applying pending session database migrations...");
                sessionDbContext.Database.Migrate();
                logger.LogInformation("Database session migrations applied successfully");
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

    AddRobotsConfiguration(builder.Configuration, app);

    logger.LogInformation("Building application pipeline...");

    // CORS must be early in the pipeline - before controllers and static files
    app.UseCors();

    app.UseAuthentication();

    app.UseRouting();
    app.UseRateLimiter();

    app.UseDefaultFiles();
    app.MapStaticAssets();

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("Development environment detected - enabling Swagger UI");
        app.UseSwagger(c =>
        {
            c.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
        });
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ArtsKart3 API v1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
            c.UseRequestInterceptor("(req) => { req.headers['X-CSRF'] = '1'; return req; }");
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
    app.MapBffManagementEndpoints();
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
    // After the beta, set Application:AllowRobotsInProduction=true on the production app only, not in appsettings.json.
    var allowIndexing = !webApplication.Environment.IsDevelopment()
        && !webApplication.Environment.IsEnvironment("Test")
        && Convert.ToBoolean(configuration["Application:AllowRobotsInProduction"]);

    webApplication.Use(async (context, next) =>
    {
        if (context.Request.Path == "/robots.txt")
        {
            context.Response.ContentType = "text/plain";

            // Pages must stay crawlable, otherwise crawlers never see the noindex header.
            await context.Response.WriteAsync(allowIndexing
                ? "User-agent: *\nAllow: /\nCrawl-delay: 1\n"
                : "User-agent: *\nDisallow: /api/\n");

            return;
        }

        if (!allowIndexing)
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex";
        }

        await next();
    });
}

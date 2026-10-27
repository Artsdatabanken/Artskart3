using System.Text;
using Azure.Identity;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Artskart3.Infrastructure.Data;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Services;
using Artskart3.Workers.Export;
using Artskart3.Workers.Import;
using Artskart3.Workers.Configuration;
using Artskart3.Infrastructure.DependencyInjection;
using Artskart3.Core.Application.Import.Writing;
using Artskart3.Infrastructure.Import.Writing;
using Artskart3.Infrastructure.Maintenance;
using Artskart3.Workers.Maintenance;
using Artskart3.Workers.Taxonomy;
using Artskart3.Core.Application.Taxonomy;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!builder.Environment.IsDevelopment())
{
    var keyVaultUrl = new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUrl, new DefaultAzureCredential());
}

var logger = LoggerFactory.Create(c => c.AddConsole().AddDebug())
    .CreateLogger("Startup");

logger.LogInformation("Artskart3.Workers - Starting up");
logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);

// Database
var dbConnectionString = builder.Configuration.GetConnectionString("ArtskartIndex");
var workerConnectionString = builder.Configuration.GetConnectionString("ArtskartIndex_Worker");
workerConnectionString = string.IsNullOrEmpty(workerConnectionString) ? dbConnectionString : workerConnectionString;

builder.Services.AddDbContext<ArtskartDbContext>(options =>
{
    options.UseSqlServer(workerConnectionString, x =>
    {
        x.UseNetTopologySuite();
        x.CommandTimeout(600);
    });
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddScoped<IArtsKartDbContext>(provider =>
    provider.GetRequiredService<ArtskartDbContext>());

// Cache-databasen til Artskart 2. Leses av importen, aldri skrevet til.
//
// Registreres uansett, slik at workeren starter på en maskin uten tilgang til cachen.
// Mangler strengen, feiler først den jobben som faktisk prøver å lese — og da har
// loggen allerede sagt hvorfor.
var cacheConnectionString = builder.Configuration.GetConnectionString("ArtskartCache");
builder.Services.AddCacheReader(cacheConnectionString);
builder.Services.AddImportServices();

if (string.IsNullOrWhiteSpace(cacheConnectionString))
{
    logger.LogWarning(
        "ConnectionStrings:ArtskartCache er ikke satt. Importjobber kan ikke lese fra cache-databasen.");
}

// Importens egne innstillinger. Standardverdiene er trygge: 250 000 poster per runde,
// alle aktive noder, og maskeringssperren slått på — se ImportOptions.
var importOptions = builder.Configuration.GetSection(ImportOptions.SectionName).Get<ImportOptions>()
                    ?? new ImportOptions();
builder.Services.AddSingleton(importOptions);

// Skrivingen har sin egen tilkobling fordi staging-tabellene lever i sesjonen og
// derfor ikke kan dele EFs. Samme database, egen forbindelse.
builder.Services.AddSingleton(new ObservationWriterOptions
{
    ConnectionString = workerConnectionString ?? string.Empty,
    BatchSize = importOptions.BatchSize
});

// Etterarbeidet — tellinger og columnstore-vedlikehold — bruker samme base, men sine
// egne tidsgrenser: setningene skanner hele Observation.
builder.Services.AddSingleton(
    builder.Configuration.GetSection(MaintenanceOptions.SectionName).Get<MaintenanceOptions>()
    ?? new MaintenanceOptions { ConnectionString = workerConnectionString ?? string.Empty });

if (importOptions.AllowImportWithoutMasking)
{
    logger.LogWarning(
        "Import:AllowImportWithoutMasking er satt. Importen skriver eksakte koordinater også for "
        + "sensitive arter, fordi maskeringen (fase 8) ikke er implementert. Dette skal bare være "
        + "satt mot testdata.");
}

// Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(dbConnectionString, new SqlServerStorageOptions
    {
        SchemaName = "Hangfire",
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        PrepareSchemaIfNecessary = true
    }));

builder.Services.AddHangfireServer();

// Konfigurasjon
builder.Services.Configure<CsvExportOptions>(
    builder.Configuration.GetSection("CsvExport"));

// Tjenester
builder.Services.AddScoped<IBlobStorageService, Artskart3.Infrastructure.Services.BlobStorageService>();
builder.Services.AddScoped<CsvWriterService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<ImportJob>();
builder.Services.AddScoped<ObservationCountJob>();
builder.Services.AddScoped<DatabaseMaintenanceJob>();

// Taksonomispeilingen. MIDLERTIDIG: den leser fra Artskart 2s RavenDB, og hele
// Artskart3.Workers/Taxonomy skal slettes når harvest-delen flytter dokumentene til
// SQL Server. Derfor er alt samlet her — én HttpClient, ett sett innstillinger, én jobb.
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Taxonomy").Get<TaxonomyOptions>() ?? new TaxonomyOptions());
builder.Services.AddHttpClient<ITaxonomySource, RavenTaxonomySource>((sp, klient) =>
{
    var innstillinger = sp.GetRequiredService<TaxonomyOptions>();

    klient.BaseAddress = new Uri(innstillinger.BaseAddress);

    // 180 000 taksoner i én strøm. Standardtimeouten på 100 sekunder holder ikke.
    klient.Timeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddScoped<TaxonomyRefreshService>();
builder.Services.AddScoped<TaxonomyRefreshJob>();
builder.Services.AddSingleton<Artskart3.Core.Application.Services.ExportColumnRegistry>();

// Taksonhierarki. Workeren trenger dette fordi taksonfilteret må utvides hierarkisk
// på nøyaktig samme måte som i API-ets forhåndstelling — ellers teller API-et ett
// utvalg og workeren skriver et annet.
//
// Registreres likt som i API-et (samme singleton bak både konkret type og
// grensesnitt, med hosted service som laster ved oppstart). Alternativet var et
// eget, lettere rangoppslag her, men to implementasjoner av det samme oppslaget
// er akkurat den avvikstypen denne endringen skal bli kvitt.
builder.Services.AddSingleton<TaxonHierarchyService>();
builder.Services.AddSingleton<ITaxonHierarchyService>(sp => sp.GetRequiredService<TaxonHierarchyService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaxonHierarchyService>());

// Helsesjekk
builder.Services.AddHealthChecks();

var app = builder.Build();

// Verifiser tilkobling til blob storage ved oppstart (f.eks. at Azurite kjører lokalt).
// CSV-eksport krever blob storage, så vi feiler raskt med en tydelig melding hvis den ikke er tilgjengelig.
await CheckBlobStorageConnectionAsync(app.Services, logger);

// Hangfire Dashboard — kun tilgjengelig i Development.
// TODO: Legg til autentisering/autorisering for andre miljøer når det er avklart med prosjektgruppen.
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
    logger.LogInformation("Hangfire Dashboard: {Url}", $"{app.Urls.FirstOrDefault() ?? "http://localhost:5035"}/hangfire");
}

app.MapHealthChecks("/health");

// Registrer recurring jobs
RegisterRecurringJobs(
    app.Services.GetRequiredService<IRecurringJobManager>(),
    app.Services.GetRequiredService<IConfiguration>());

logger.LogInformation("Artskart3.Workers - Started successfully");

app.Run();

async Task CheckBlobStorageConnectionAsync(IServiceProvider services, ILogger startupLogger)
{
    using var scope = services.CreateScope();
    var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
    try
    {
        await blobStorage.CheckConnectionAsync();
        startupLogger.LogInformation("Blob storage-tilkobling verifisert.");
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex,
            "Kan ikke koble til blob storage ved oppstart. " +
            "Eksportjobber vil feile med en gang til blob storage er tilgjengelig igjen.");
    }
}

void RegisterRecurringJobs(IRecurringJobManager recurringJobManager, IConfiguration configuration)
{
    var schedules = configuration.GetSection("Hangfire:ScheduleDefaults");

    // CSV-eksport — poller databasen for ventende jobber
    var csvExportCron = schedules["CsvExportPoll"] ?? "*/30 * * * * *";
    recurringJobManager.AddOrUpdate<CsvExportPollJob>(
        "csv-export-poll",
        job => job.ExecuteAsync(CancellationToken.None),
        csvExportCron);

    // Opprydding av utløpte eksportjobber — kjører daglig kl. 03:00
    var cleanupCron = schedules["CsvExportCleanup"] ?? "0 3 * * *";
    recurringJobManager.AddOrUpdate<CsvExportCleanupJob>(
        "csv-export-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        cleanupCron);

    // Fremtidige jobber (harvest, import, vedlikehold) registreres her
}

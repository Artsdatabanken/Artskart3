using System.Text;
using Azure.Identity;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Artskart3.Infrastructure.Data;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Workers.Export;
using Artskart3.Workers.Configuration;

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
builder.Services.AddSingleton<Artskart3.Core.Application.Services.ExportColumnRegistry>();

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

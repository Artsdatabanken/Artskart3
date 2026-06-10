using Azure.Identity;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Artskart3.Infrastructure.Data;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Workers.Export;
using Artskart3.Workers.Configuration;

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
var workerConnectionString = builder.Configuration.GetConnectionString("ArtskartIndex_Worker") ?? dbConnectionString;

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
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<CsvWriterService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<ExportColumnRegistry>();

// Helsesjekk
builder.Services.AddHealthChecks();

var app = builder.Build();

// Hangfire Dashboard — ingen autentisering foreløpig
app.MapHangfireDashboard("/hangfire");

app.MapHealthChecks("/health");

// Registrer recurring jobs
RegisterRecurringJobs(app.Services.GetRequiredService<IConfiguration>());

logger.LogInformation("Artskart3.Workers - Started successfully");

app.Run();

void RegisterRecurringJobs(IConfiguration configuration)
{
    var schedules = configuration.GetSection("Hangfire:ScheduleDefaults");

    // CSV-eksport — poller databasen for ventende jobber
    var csvExportCron = schedules["CsvExportPoll"] ?? "*/30 * * * * *";
    RecurringJob.AddOrUpdate<CsvExportPollJob>(
        "csv-export-poll",
        job => job.ExecuteAsync(CancellationToken.None),
        csvExportCron);

    // Fremtidige jobber (harvest, import, vedlikehold) registreres her
}

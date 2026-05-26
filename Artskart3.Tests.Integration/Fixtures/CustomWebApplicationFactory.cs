using Artskart3.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Artskart3.Tests.Integration.Fixtures;

/// <summary>
/// Tilpasset WebApplicationFactory som kobler testens SQL Server-container
/// til den fullstendige ASP.NET Core-pipeline.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // Deaktiver automatisk migrasjon så Program.cs ikke kaller Database.Migrate() mot
        // testcontaineren. EnsureCreatedAsync() i DatabaseFixture har allerede opprettet
        // hele skjemaet; å kjøre migrasjoner oppå dette vil feile fordi migrasjons-
        // historikken er tom og InitialRefactor vil forsøke å gjenopprette eksisterende objekter.
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            // Erstatt registrert DbContext med en som peker mot testcontaineren
            services.RemoveAll<DbContextOptions<ArtskartDbContext>>();
            services.RemoveAll<ArtskartDbContext>();

            services.AddDbContext<ArtskartDbContext>(options =>
            {
                options.UseSqlServer(_connectionString, x => x.UseNetTopologySuite());
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
        });
    }
}

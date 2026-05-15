using Artskart3.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Artskart3.Tests.Integration.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that wires the test SQL Server container's
/// connection string into the full ASP.NET Core pipeline.
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

        // Disable auto-migration so Program.cs doesn't call Database.Migrate() against
        // the test container. EnsureCreatedAsync() in DatabaseFixture already created
        // the full schema; running migrations on top of it would fail because migration
        // history is empty and InitialRefactor would try to re-create existing objects.
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            // Replace the registered DbContext with one pointing at the test container
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

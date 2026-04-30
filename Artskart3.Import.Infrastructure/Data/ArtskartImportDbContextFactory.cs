using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Import.Infrastructure.Data;

/// <summary>
/// Used by EF Core tooling (dotnet ef migrations add, dotnet ef database update) at design time.
/// Reads the connection string from appsettings.json in the API project.
/// </summary>
public class ArtskartImportDbContextFactory : IDesignTimeDbContextFactory<ArtskartImportDbContext>
{
    public ArtskartImportDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Artskart3.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("ArtskartImportDb")
            ?? throw new InvalidOperationException("Connection string 'ArtskartImportDb' not found in appsettings.json.");

        var optionsBuilder = new DbContextOptionsBuilder<ArtskartImportDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ArtskartImportDbContext(optionsBuilder.Options);
    }
}

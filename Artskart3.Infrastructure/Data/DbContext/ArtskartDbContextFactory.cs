using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Infrastructure.Data
{
    /// <summary>
    /// Used by EF Core tooling (dotnet ef migrations add, dotnet ef database update) at design time.
    /// Reads the connection string from appsettings.json in the API project.
    /// </summary>
    public class ArtskartDbContextFactory : IDesignTimeDbContextFactory<ArtskartDbContext>
    {
        public ArtskartDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Artskart3.Api"))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString("ArtskartDb")
                ?? throw new InvalidOperationException("Connection string 'ArtskartDb' not found in appsettings.json.");

            var optionsBuilder = new DbContextOptionsBuilder<ArtskartDbContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlOptions => sqlOptions.UseNetTopologySuite());

            return new ArtskartDbContext(optionsBuilder.Options);
        }
    }
}

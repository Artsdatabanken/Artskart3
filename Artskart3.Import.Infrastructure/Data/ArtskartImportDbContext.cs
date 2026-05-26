using Artskart3.Import.Core.Domain.Entities;
using Artskart3.Import.Infrastructure.Data.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Import.Infrastructure.Data;

public class ArtskartImportDbContext : DbContext
{
    public ArtskartImportDbContext()
    {
    }

    public ArtskartImportDbContext(DbContextOptions<ArtskartImportDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DataSource> DataSources { get; set; }
    public virtual DbSet<GbifDatasetDiscovery> GbifDatasetDiscoveries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DataSourceConfiguration());
        modelBuilder.ApplyConfiguration(new GbifDatasetDiscoveryConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}

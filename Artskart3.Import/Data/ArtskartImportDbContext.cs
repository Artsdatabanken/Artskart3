using Artskart3.Import.Data.EntityConfigurations;
using Artskart3.Import.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Import.Data;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DataSourceConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}

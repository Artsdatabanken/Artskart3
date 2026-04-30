using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Artskart3.Infrastructure.Data.EntityConfigurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("DataSource");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ProviderType)
            .IsRequired();

        builder.Property(e => e.RemoteAddress)
            .HasMaxLength(1000);

        builder.Property(e => e.ArchiveName)
            .HasMaxLength(500);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Large JSON columns — no length limit
        builder.Property(e => e.GbifApiQuery)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.HarvestPropertyOverrides)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.ImportPropertyOverrides)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Doi)
            .HasMaxLength(200);

        builder.HasIndex(e => e.ProviderType)
            .HasDatabaseName("IX_DataSource_ProviderType");

        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("IX_DataSource_IsDeleted");
    }
}

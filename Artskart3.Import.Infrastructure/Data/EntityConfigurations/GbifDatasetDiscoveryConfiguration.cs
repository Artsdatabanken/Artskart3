using Artskart3.Import.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Artskart3.Import.Infrastructure.Data.EntityConfigurations;

public class GbifDatasetDiscoveryConfiguration : IEntityTypeConfiguration<GbifDatasetDiscovery>
{
    public void Configure(EntityTypeBuilder<GbifDatasetDiscovery> builder)
    {
        builder.ToTable("GbifDatasetDiscovery");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.GbifId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.PublishingOrganizationKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.HomepageUrl)
            .HasMaxLength(1000);

        builder.Property(e => e.DwcArchiveUrl)
            .HasMaxLength(1000);

        builder.Property(e => e.Doi)
            .HasMaxLength(200);

        // Stored as a JSON array
        builder.Property(e => e.Identifiers)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Status)
            .IsRequired();

        builder.HasIndex(e => e.GbifId)
            .IsUnique()
            .HasDatabaseName("IX_GbifDatasetDiscovery_GbifId");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_GbifDatasetDiscovery_Status");
    }
}

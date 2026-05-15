using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Artskart3.Infrastructure.Data.EntityConfigurations
{
    /// <summary>
    /// Entity configuration for Observation table to optimize search queries.
    /// Defines indexes on frequently filtered columns and composite indexes for common filter combinations.
    /// Indexes that already exist in OnModelCreating are referenced by name so EF merges them
    /// rather than creating duplicates.
    /// </summary>
    public class ObservationSearchIndexConfiguration : IEntityTypeConfiguration<Observation>
    {
        public void Configure(EntityTypeBuilder<Observation> builder)
        {
            // Single column indexes for individual filters
            builder.HasIndex(o => o.LocationId, "IX_Observation_LocationId");
            builder.HasIndex(o => o.TaxonGroupId, "IX_Observation_TaxonGroupId");
            builder.HasIndex(o => o.MonthCollected, "IX_Observation_MonthCollected");
            builder.HasIndex(o => o.CategoryId, "IX_Observation_CategoryId");
            builder.HasIndex(o => o.BasisOfRecordId, "IX_Observation_BasisOfRecordId");
            builder.HasIndex(o => o.InstitutionId, "IX_Observation_InstitutionId");
            builder.HasIndex(o => o.InstitutionCode, "IX_Observation_InstitutionCode");
            builder.HasIndex(o => o.YearCollected, "IX_Observation_YearCollected");
            builder.HasIndex(o => o.CoordinatePrecisionInMeters, "IX_Observation_CoordinatePrecisionInMeters");
            builder.HasIndex(o => o.DateLastModified, "IX_Observation_DateLastModified");

            // Composite indexes for common filter combinations
            builder.HasIndex(o => new { o.LocationId, o.HasErrors, o.HasAnnotations }, "IX_Observation_LocationId_HasErrors_HasAnnotations");
            builder.HasIndex(o => new { o.YearCollected, o.LocationId }, "IX_Observation_YearCollected_LocationId");
            builder.HasIndex(o => new { o.TaxonGroupId, o.LocationId }, "IX_Observation_TaxonGroupId_LocationId");
        }
    }
}

using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Artskart3.Infrastructure.Data.EntityConfigurations
{
    /// <summary>
    /// Entity configuration for Observation table to optimize search queries.
    /// Defines indexes on frequently filtered columns and composite indexes for common filter combinations.
    /// </summary>
    public class ObservationSearchIndexConfiguration : IEntityTypeConfiguration<Observation>
    {
        public void Configure(EntityTypeBuilder<Observation> builder)
        {
            // Single column indexes for individual filters
            builder.HasIndex(o => o.LocationId)
                .HasDatabaseName("IX_Observation_LocationId");

            builder.HasIndex(o => o.TaxonGroupId)
                .HasDatabaseName("IX_Observation_TaxonGroupId");

            builder.HasIndex(o => o.MonthCollected)
                .HasDatabaseName("IX_Observation_MonthCollected");

            builder.HasIndex(o => o.CategoryId)
                .HasDatabaseName("IX_Observation_CategoryId");

            builder.HasIndex(o => o.BasisOfRecordId)
                .HasDatabaseName("IX_Observation_BasisOfRecordId");

            builder.HasIndex(o => o.InstitutionId)
                .HasDatabaseName("IX_Observation_InstitutionId");

            builder.HasIndex(o => o.InstitutionCode)
                .HasDatabaseName("IX_Observation_InstitutionCode");

            builder.HasIndex(o => o.YearCollected)
                .HasDatabaseName("IX_Observation_YearCollected");

            builder.HasIndex(o => o.CoordinatePrecisionInMeters)
                .HasDatabaseName("IX_Observation_CoordinatePrecisionInMeters");

            builder.HasIndex(o => o.DateLastModified)
                .HasDatabaseName("IX_Observation_DateLastModified");

            // Composite indexes for common filter combinations
            
            /// <summary>
            /// Composite index optimizing queries that filter by location and flag columns.
            /// Improves performance for HasErrors, HasAnnotations, IsSensitive filters.
            /// </summary>
            builder.HasIndex(o => new { o.LocationId, o.HasErrors, o.HasAnnotations })
                .HasDatabaseName("IX_Observation_LocationId_HasErrors_HasAnnotations");

            /// <summary>
            /// Composite index optimizing year range filtering with location grouping.
            /// Improves performance for YearFrom/YearTo filters grouped by location.
            /// </summary>
            builder.HasIndex(o => new { o.YearCollected, o.LocationId })
                .HasDatabaseName("IX_Observation_YearCollected_LocationId");

            /// <summary>
            /// Composite index optimizing taxon filtering with location grouping.
            /// Improves performance for TaxonGroupId filters grouped by location.
            /// </summary>
            builder.HasIndex(o => new { o.TaxonGroupId, o.LocationId })
                .HasDatabaseName("IX_Observation_TaxonGroupId_LocationId");
        }
    }
}

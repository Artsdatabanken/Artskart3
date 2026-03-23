using System;
using Microsoft.EntityFrameworkCore;
using Artskart3.Core.Domain.Entities;

using Artskart3.Infrastructure.Persistence.Repositories;
namespace Artskart3.Infrastructure.Data
{
    public partial class ArtskartDbContext : DbContext, IArtsKartDbContext
    {
    public ArtskartDbContext()
    {
    }

    public ArtskartDbContext(DbContextOptions<ArtskartDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<AreaType> AreaTypes { get; set; }

    public virtual DbSet<BasisOfRecord> BasisOfRecords { get; set; }

    public virtual DbSet<Behavior> Behaviors { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CategoryType> CategoryTypes { get; set; }

    public virtual DbSet<CommandLog> CommandLogs { get; set; }

    public virtual DbSet<DeletedItem> DeletedItems { get; set; }

    public virtual DbSet<ExportStatus> ExportStatuses { get; set; }

    public virtual DbSet<Fab4exclude> Fab4excludes { get; set; }

    public virtual DbSet<Filter> Filters { get; set; }

    public virtual DbSet<GeometryColumn> GeometryColumns { get; set; }

    public virtual DbSet<ImportLog> ImportLogs { get; set; }

    public virtual DbSet<ImportNotification> ImportNotifications { get; set; }

    public virtual DbSet<ImportState> ImportStates { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Maskeringsruter16x16km> Maskeringsruter16x16kms { get; set; }

    public virtual DbSet<Maskeringsruter4x4km> Maskeringsruter4x4kms { get; set; }

    public virtual DbSet<Maskeringsruter8x8km> Maskeringsruter8x8kms { get; set; }

    public virtual DbSet<MediaFile> MediaFiles { get; set; }

    public virtual DbSet<MigrationHistory> MigrationHistories { get; set; }

    public virtual DbSet<Observation> Observations { get; set; }

    public virtual DbSet<ObservationDetail> ObservationDetails { get; set; }

    public virtual DbSet<ObservationError> ObservationErrors { get; set; }

    public virtual DbSet<ObservationLink> ObservationLinks { get; set; }

    public virtual DbSet<ObservationQualityType> ObservationQualityTypes { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationRelation> OrganizationRelations { get; set; }

    public virtual DbSet<OrganizationRelationType> OrganizationRelationTypes { get; set; }

    public virtual DbSet<OrganizationType> OrganizationTypes { get; set; }

    public virtual DbSet<ProcessRecordResult> ProcessRecordResults { get; set; }

    public virtual DbSet<ProcessSourceDataResult> ProcessSourceDataResults { get; set; }

    public virtual DbSet<ProsessEngineHistory> ProsessEngineHistories { get; set; }

    public virtual DbSet<RecordNotificationType> RecordNotificationTypes { get; set; }

    public virtual DbSet<RecordValidationType> RecordValidationTypes { get; set; }

    public virtual DbSet<RejectedRecord> RejectedRecords { get; set; }

    public virtual DbSet<SensitiveObservationDatum> SensitiveObservationData { get; set; }

    public virtual DbSet<SpatialRefSy> SpatialRefSys { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Taxon> Taxons { get; set; }

    public virtual DbSet<TaxonGroup> TaxonGroups { get; set; }

    public virtual DbSet<TaxonName> TaxonNames { get; set; }

    public virtual DbSet<TaxonPopularName> TaxonPopularNames { get; set; }

    public virtual DbSet<TaxonProperty> TaxonProperties { get; set; }

    public virtual DbSet<TaxonRank> TaxonRanks { get; set; }

    public virtual DbSet<TaxonomyState> TaxonomyStates { get; set; }

    // Removed OnConfiguring to use DI-based configuration from Program.cs

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Area");

            entity.ToTable("Area");

            entity.HasIndex(e => new { e.AreaTypeId, e.Id, e.Fid }, "IX_AreaTypeID");

            entity.HasIndex(e => e.Fid, "IX_Fid");

            entity.HasIndex(e => e.ParentFid, "IX_ParentFid");

            entity.HasIndex(e => e.Name, "NonClusteredIndex-20180305-111522");

            entity.Property(e => e.AreaTypeId).HasColumnName("AreaTypeID");
            entity.Property(e => e.Bbox).HasMaxLength(50);
            entity.Property(e => e.DocumentId).HasMaxLength(200);
            entity.Property(e => e.Fid).HasMaxLength(50);
            entity.Property(e => e.GmBbox).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ParentFid).HasMaxLength(50);
            entity.Property(e => e.SyncDateTime).HasColumnType("datetime");
            entity.Property(e => e.TimeStamp)
                .HasDefaultValue(new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .HasColumnType("datetime");

            entity.HasOne(d => d.AreaType).WithMany(p => p.Areas)
                .HasForeignKey(d => d.AreaTypeId)
                .HasConstraintName("FK_dbo.Area_dbo.AreaType_AreaTypeID");
        });

        modelBuilder.Entity<AreaType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.AreaType");

            entity.ToTable("AreaType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CategoryName).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<BasisOfRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.BasisOfRecord");

            entity.ToTable("BasisOfRecord");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(30);
            entity.Property(e => e.Name).HasMaxLength(60);
            entity.Property(e => e.Variants).HasMaxLength(300);
        });

        modelBuilder.Entity<Behavior>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Behavior");

            entity.ToTable("Behavior");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(60);
            entity.Property(e => e.Variants).HasMaxLength(300);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Category");

            entity.ToTable("Category");

            entity.HasIndex(e => e.CategoryTypeId, "IX_CategoryTypeId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code).HasMaxLength(2);
            entity.Property(e => e.Name).HasMaxLength(50);

            entity.HasOne(d => d.CategoryType).WithMany(p => p.Categories)
                .HasForeignKey(d => d.CategoryTypeId)
                .HasConstraintName("FK_dbo.Category_dbo.CategoryType_CategoryTypeId");
        });

        modelBuilder.Entity<CategoryType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.CategoryType");

            entity.ToTable("CategoryType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(60);
        });

        modelBuilder.Entity<CommandLog>(entity =>
        {
            entity.ToTable("CommandLog");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CommandType).HasMaxLength(60);
            entity.Property(e => e.DatabaseName).HasMaxLength(128);
            entity.Property(e => e.ExtendedInfo).HasColumnType("xml");
            entity.Property(e => e.IndexName).HasMaxLength(128);
            entity.Property(e => e.ObjectName).HasMaxLength(128);
            entity.Property(e => e.ObjectType)
                .HasMaxLength(2)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.SchemaName).HasMaxLength(128);
            entity.Property(e => e.StatisticsName).HasMaxLength(128);
        });

        modelBuilder.Entity<DeletedItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.DeletedItem");

            entity.ToTable("DeletedItem");

            entity.HasIndex(e => e.RecordId, "Ix_RecordId");

            entity.HasIndex(e => e.TimeStamp, "Ix_TimeStamp");

            entity.HasIndex(e => e.TimeStamp, "Ix_TimeStampWithRecordId");

            entity.Property(e => e.RecordId).HasMaxLength(255);
            entity.Property(e => e.TimeStamp).HasColumnType("datetime");
        });

        modelBuilder.Entity<ExportStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ExportStatus");

            entity.ToTable("ExportStatus");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Fab4exclude>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.FAB4Exclude");

            entity.ToTable("FAB4Exclude");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Filter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Filter");

            entity.ToTable("Filter");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.SerializedFilter).HasMaxLength(3700);
        });

        modelBuilder.Entity<GeometryColumn>(entity =>
        {
            entity.HasKey(e => new { e.FTableCatalog, e.FTableSchema, e.FTableName, e.FGeometryColumn }).HasName("geometry_columns_pk");

            entity.ToTable("geometry_columns");

            entity.Property(e => e.FTableCatalog)
                .HasMaxLength(128)
                .IsUnicode(false)
                .HasColumnName("f_table_catalog");
            entity.Property(e => e.FTableSchema)
                .HasMaxLength(128)
                .IsUnicode(false)
                .HasColumnName("f_table_schema");
            entity.Property(e => e.FTableName)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("f_table_name");
            entity.Property(e => e.FGeometryColumn)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("f_geometry_column");
            entity.Property(e => e.CoordDimension).HasColumnName("coord_dimension");
            entity.Property(e => e.GeometryType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("geometry_type");
            entity.Property(e => e.Srid).HasColumnName("srid");
        });

        modelBuilder.Entity<ImportLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ImportLog");

            entity.ToTable("ImportLog");

            entity.HasIndex(e => e.Timestamp, "Ix_Time");

            entity.Property(e => e.ModeCommand).HasColumnName("Mode_Command");
            entity.Property(e => e.ModeDataset).HasColumnName("Mode_Dataset");
            entity.Property(e => e.ModeFilePath).HasColumnName("Mode_FilePath");
            entity.Property(e => e.ModePartitionYear).HasColumnName("Mode_PartitionYear");
            entity.Property(e => e.ModeProcessMode).HasColumnName("Mode_ProcessMode");
            entity.Property(e => e.ModeSensitiveRecords).HasColumnName("Mode_SensitiveRecords");
        });

        modelBuilder.Entity<ImportNotification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ImportNotification");

            entity.ToTable("ImportNotification");

            entity.HasIndex(e => e.Timestamp, "IX_TimeStamp");

            entity.Property(e => e.SeverityDescription).HasMaxLength(100);
            entity.Property(e => e.Subject).HasMaxLength(250);
        });

        modelBuilder.Entity<ImportState>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ImportState");

            entity.ToTable("ImportState");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Doi).HasMaxLength(255);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Location");

            entity.ToTable("Location");

            entity.HasIndex(e => e.CoordinatePrecision, "IX_CoordinatePrecision");

            entity.HasIndex(e => new { e.East, e.North }, "IX_EastNorth");

            entity.HasIndex(e => e.East, "IX_EastNorthGeom");

            entity.HasIndex(e => e.LookupId, "IX_LookupId");

            entity.Property(e => e.Locality).HasMaxLength(1000);
            entity.Property(e => e.LocationId).HasMaxLength(50);
            entity.Property(e => e.LookupId).HasMaxLength(50);
            entity.Property(e => e.TimeStamp).HasColumnType("datetime");

            entity.HasMany(d => d.Areas).WithMany(p => p.Locations)
                .UsingEntity<Dictionary<string, object>>(
                    "LocationArea",
                    r => r.HasOne<Area>().WithMany()
                        .HasForeignKey("AreaId")
                        .HasConstraintName("FK_dbo.LocationAreas_dbo.Area_AreaId"),
                    l => l.HasOne<Location>().WithMany()
                        .HasForeignKey("LocationId")
                        .HasConstraintName("FK_dbo.LocationAreas_dbo.Location_LocationId"),
                    j =>
                    {
                        j.HasKey("LocationId", "AreaId").HasName("PK_dbo.LocationAreas");
                        j.ToTable("LocationAreas");
                        j.HasIndex(new[] { "AreaId" }, "IX_AreaId");
                        j.HasIndex(new[] { "LocationId" }, "IX_LocationId");
                        j.HasIndex(new[] { "LocationId", "AreaId" }, "IX_LocationIdArea");
                    });
        });

        modelBuilder.Entity<Maskeringsruter16x16km>(entity =>
        {
            entity.HasKey(e => e.Objectid);

            entity.ToTable("Maskeringsruter_16x16km");

            entity.Property(e => e.Objectid).HasColumnName("OBJECTID");
            entity.Property(e => e.KriteriumMaskeringsrute).HasColumnName("KRITERIUM_MASKERINGSRUTE");
            entity.Property(e => e.Ruteid)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("RUTEID");
        });

        modelBuilder.Entity<Maskeringsruter4x4km>(entity =>
        {
            entity.HasKey(e => e.Objectid);

            entity.ToTable("Maskeringsruter_4x4km");

            entity.Property(e => e.Objectid).HasColumnName("OBJECTID");
            entity.Property(e => e.KriteriumMaskeringsrute).HasColumnName("KRITERIUM_MASKERINGSRUTE");
            entity.Property(e => e.Ruteid)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("RUTEID");
        });

        modelBuilder.Entity<Maskeringsruter8x8km>(entity =>
        {
            entity.HasKey(e => e.Objectid);

            entity.ToTable("Maskeringsruter_8x8km");

            entity.Property(e => e.Objectid).HasColumnName("OBJECTID");
            entity.Property(e => e.KriteriumMaskeringsrute).HasColumnName("KRITERIUM_MASKERINGSRUTE");
            entity.Property(e => e.Ruteid)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("RUTEID");
        });

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.MediaFile");

            entity.ToTable("MediaFile");

            entity.HasIndex(e => new { e.Downloaded, e.MediaFileTypeId, e.DownloadRetryCount }, "IX_DownLoaded");

            entity.HasIndex(e => e.ObservationId, "IX_Observation_Id");

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.License).HasMaxLength(500);
            entity.Property(e => e.ObservationId).HasColumnName("Observation_Id");
            entity.Property(e => e.Origin).HasMaxLength(2000);
            entity.Property(e => e.RightsHolder).HasMaxLength(500);

            entity.HasOne(d => d.MediaFileType).WithMany(p => p.MediaFiles)
                .HasForeignKey(d => d.MediaFileTypeId)
                .HasConstraintName("FK_dbo.MediaFile_dbo.MediaFileType_MediaFileTypeId");

            entity.HasOne(d => d.Observation).WithMany(p => p.MediaFiles)
                .HasForeignKey(d => d.ObservationId)
                .HasConstraintName("FK_dbo.MediaFile_dbo.Observation_Observation_Id");
        });

        modelBuilder.Entity<MediaFileType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.MediaFileType");

            entity.ToTable("MediaFileType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.MediaTypeName).HasMaxLength(20);
            entity.Property(e => e.MimeType).HasMaxLength(20);
        });

        modelBuilder.Entity<MigrationHistory>(entity =>
        {
            entity.HasKey(e => new { e.MigrationId, e.ContextKey }).HasName("PK_dbo.__MigrationHistory");

            entity.ToTable("__MigrationHistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ContextKey).HasMaxLength(300);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

        modelBuilder.Entity<Observation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Observation");

            entity.ToTable("Observation");

            entity.HasIndex(e => e.BasisOfRecordId, "IX_BasisOfRecordId");

            entity.HasIndex(e => e.CatalogNumber, "IX_CatalogNumber");

            entity.HasIndex(e => new { e.CategoryId, e.DateTimeRecordImported }, "IX_CategoryId");

            entity.HasIndex(e => e.DateTimeCollected, "IX_DateTimeCollected").IsDescending();

            entity.HasIndex(e => e.DateTimeRecordImported, "IX_DateTimeRecordImported");

            entity.HasIndex(e => e.DatetimeIdentified, "IX_DatetimeIdentified");

            entity.HasIndex(e => new { e.LocationId, e.Id }, "IX_LocationCatProxy");

            entity.HasIndex(e => new { e.LocationId, e.CategoryId }, "IX_LocationId");

            entity.HasIndex(e => e.MatchedScientificNameId, "IX_MatchedScientificName");

            entity.HasIndex(e => e.MonthCollected, "IX_MonthCollected");

            entity.HasIndex(e => e.NodeId, "IX_NodeId");

            entity.HasIndex(e => e.ObservationQualityTypeId, "IX_ObservationQualityTypeId");

            entity.HasIndex(e => e.OccurenceId, "IX_OccurenceId");

            entity.HasIndex(e => e.ProxyId, "IX_ProxyId");

            entity.HasIndex(e => new { e.TaxonId, e.Id, e.CoordinatePrecisionInMeters, e.YearCollected }, "IX_Rodliste");

            entity.HasIndex(e => new { e.TaxonGroupId, e.CategoryId }, "IX_TaxonGroupId");

            entity.HasIndex(e => new { e.TaxonGroupId, e.LocationId }, "IX_TaxonGroupIdLocationId");

            entity.HasIndex(e => new { e.TaxonId, e.MatchedScientificNameId }, "IX_TaxonId");

            entity.HasIndex(e => new { e.YearCollected, e.MonthCollected }, "IX_YearCollected");

            entity.HasIndex(e => e.CoordinatePrecisionInMeters, "Ix_CoordPrec");

            entity.HasIndex(e => e.TaxonGroupId, "TaxonGroupId");

            entity.Property(e => e.CatalogNumber).HasMaxLength(200);
            entity.Property(e => e.CollectionCode).HasMaxLength(100);
            entity.Property(e => e.DateTimeRecordProsessed).HasColumnType("datetime");
            entity.Property(e => e.InstitutionCode).HasMaxLength(100);
            entity.Property(e => e.InstitutionId).HasMaxLength(25);
            entity.Property(e => e.MonthCollected).HasComputedColumnSql("(datepart(month,[DateTimeCollected]))", false);
            entity.Property(e => e.OccurenceId).HasMaxLength(255);
            entity.Property(e => e.ProxyId).HasMaxLength(255);
            entity.Property(e => e.YearCollected).HasComputedColumnSql("(datepart(year,[DateTimeCollected]))", false);

            entity.HasOne(d => d.BasisOfRecord).WithMany(p => p.Observations)
                .HasForeignKey(d => d.BasisOfRecordId)
                .HasConstraintName("FK_dbo.Observation_dbo.BasisOfRecord_BasisOfRecordId");

            entity.HasOne(d => d.Category).WithMany(p => p.Observations)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_dbo.Observation_dbo.Category_CategoryId");

            entity.HasOne(d => d.Location).WithMany(p => p.Observations)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("FK_dbo.Observation_dbo.Location_LocationId");

            entity.HasOne(d => d.MatchedScientificName).WithMany(p => p.Observations)
                .HasForeignKey(d => d.MatchedScientificNameId)
                .HasConstraintName("FK_dbo.Observation_dbo.TaxonName_MatchedScientificNameId");

            entity.HasOne(d => d.ObservationQualityType).WithMany(p => p.Observations)
                .HasForeignKey(d => d.ObservationQualityTypeId)
                .HasConstraintName("FK_dbo.Observation_dbo.ObservationQualityType_ObservationQualityTypeId");

            entity.HasOne(d => d.Taxon).WithMany(p => p.Observations)
                .HasForeignKey(d => d.TaxonId)
                .HasConstraintName("FK_dbo.Observation_dbo.Taxon_TaxonId");

            entity.HasMany(d => d.Behaviors).WithMany(p => p.Observations)
                .UsingEntity<Dictionary<string, object>>(
                    "ObservationBehavior",
                    r => r.HasOne<Behavior>().WithMany()
                        .HasForeignKey("BehaviorId")
                        .HasConstraintName("FK_dbo.ObservationBehaviors_dbo.Behavior_BehaviorId"),
                    l => l.HasOne<Observation>().WithMany()
                        .HasForeignKey("ObservationId")
                        .HasConstraintName("FK_dbo.ObservationBehaviors_dbo.Observation_ObservationId"),
                    j =>
                    {
                        j.HasKey("ObservationId", "BehaviorId").HasName("PK_dbo.ObservationBehaviors");
                        j.ToTable("ObservationBehaviors");
                        j.HasIndex(new[] { "BehaviorId" }, "IX_BehaviorId");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Observations)
                .UsingEntity<Dictionary<string, object>>(
                    "ObservationTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("FK_dbo.ObservationTags_dbo.Tag_TagId"),
                    l => l.HasOne<Observation>().WithMany()
                        .HasForeignKey("ObservationId")
                        .HasConstraintName("FK_dbo.ObservationTags_dbo.Observation_ObservationId"),
                    j =>
                    {
                        j.HasKey("ObservationId", "TagId").HasName("PK_dbo.ObservationTags");
                        j.ToTable("ObservationTags");
                        j.HasIndex(new[] { "ObservationId" }, "IX_ObservationId");
                        j.HasIndex(new[] { "TagId" }, "IX_TagId");
                        j.HasIndex(new[] { "TagId", "ObservationId" }, "IX_TagIdObsId");
                    });

            entity.HasMany(d => d.Taxons).WithMany(p => p.ObservationsNavigation)
                .UsingEntity<Dictionary<string, object>>(
                    "ObservationTaxon",
                    r => r.HasOne<Taxon>().WithMany()
                        .HasForeignKey("TaxonId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_dbo.ObservationTaxons_dbo.Taxon_TaxonId"),
                    l => l.HasOne<Observation>().WithMany()
                        .HasForeignKey("ObservationId")
                        .HasConstraintName("FK_dbo.ObservationTaxons_dbo.Observation_ObservationId"),
                    j =>
                    {
                        j.HasKey("ObservationId", "TaxonId").HasName("PK_dbo.ObservationTaxons");
                        j.ToTable("ObservationTaxons");
                        j.HasIndex(new[] { "TaxonId", "ObservationId" }, "IX_TaxonId");
                    });
        });

        modelBuilder.Entity<ObservationDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ObservationDetails");

            entity.HasIndex(e => e.Id, "IX_Id");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CollectingMethod).HasMaxLength(100);
            entity.Property(e => e.Collector).HasMaxLength(250);
            entity.Property(e => e.DatasetId).HasMaxLength(250);
            entity.Property(e => e.DatasetName).HasMaxLength(1000);
            entity.Property(e => e.DateTimeCollectedStr).HasMaxLength(15);
            entity.Property(e => e.EventTime).HasMaxLength(100);
            entity.Property(e => e.FieldNumber).HasMaxLength(100);
            entity.Property(e => e.GeoreferenceRemarks).HasMaxLength(50);
            entity.Property(e => e.Habitat).HasMaxLength(2000);
            entity.Property(e => e.IdentifiedBy).HasMaxLength(100);
            entity.Property(e => e.IndividualCount).HasMaxLength(100);
            entity.Property(e => e.Locality).HasMaxLength(100);
            entity.Property(e => e.MeasurementMethod).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(3500);
            entity.Property(e => e.OtherCatalogNumbers).HasMaxLength(200);
            entity.Property(e => e.Preparations).HasMaxLength(200);
            entity.Property(e => e.RecordNumber).HasMaxLength(50);
            entity.Property(e => e.RelatedResourceId).HasMaxLength(50);
            entity.Property(e => e.RelationshipOfResource).HasMaxLength(50);
            entity.Property(e => e.Sex).HasMaxLength(50);
            entity.Property(e => e.TypeStatus).HasMaxLength(50);
            entity.Property(e => e.VerbatimDepth).HasMaxLength(100);

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.ObservationDetail)
                .HasForeignKey<ObservationDetail>(d => d.Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.ObservationDetails_dbo.Observation_Id");
        });

        modelBuilder.Entity<ObservationError>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ObservationError");

            entity.ToTable("ObservationError");

            entity.HasIndex(e => e.AnnotationId, "IX_AnnotationId");

            entity.HasIndex(e => e.ObservationId, "IX_ObservationId");

            entity.Property(e => e.DateTimeModified).HasColumnType("datetime");
            entity.Property(e => e.ErrorContext).HasMaxLength(400);

            entity.HasOne(d => d.Observation).WithMany(p => p.ObservationErrors)
                .HasForeignKey(d => d.ObservationId)
                .HasConstraintName("FK_dbo.ObservationError_dbo.Observation_ObservationId");
        });

        modelBuilder.Entity<ObservationLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ObservationLink");

            entity.ToTable("ObservationLink");

            entity.HasIndex(e => e.LinkedObservationId, "IX_LinkedObservationId");

            entity.HasIndex(e => e.ObservationId, "IX_ObservationId");

            entity.HasOne(d => d.LinkedObservation).WithMany(p => p.ObservationLinkLinkedObservations)
                .HasForeignKey(d => d.LinkedObservationId)
                .HasConstraintName("FK_dbo.ObservationLink_dbo.Observation_LinkedObservationId");

            entity.HasOne(d => d.Observation).WithMany(p => p.ObservationLinkObservations)
                .HasForeignKey(d => d.ObservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.ObservationLink_dbo.Observation_ObservationId");
        });

        modelBuilder.Entity<ObservationQualityType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ObservationQualityType");

            entity.ToTable("ObservationQualityType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Value).HasMaxLength(200);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Organization");

            entity.ToTable("Organization");

            entity.HasIndex(e => e.ExternalId, "IX_ExternalId");

            entity.HasIndex(e => e.OrganizationTypeId, "IX_OrganizationTypeId");

            entity.HasIndex(e => e.ParentId, "IX_ParentId");

            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.DateCreated).HasColumnType("datetime");
            entity.Property(e => e.DateModified).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(500);

            entity.HasOne(d => d.OrganizationType).WithMany(p => p.Organizations)
                .HasForeignKey(d => d.OrganizationTypeId)
                .HasConstraintName("FK_dbo.Organization_dbo.OrganizationType_OrganizationTypeId");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK_dbo.Organization_dbo.Organization_ParentId");
        });

        modelBuilder.Entity<OrganizationRelation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.OrganizationRelation");

            entity.ToTable("OrganizationRelation");

            entity.HasIndex(e => e.ObservationId, "IX_ObservationId");

            entity.HasIndex(e => e.OrganizationId, "IX_OrganizationId");

            entity.HasIndex(e => e.RelationTypeId, "IX_RelationTypeId");

            entity.HasOne(d => d.Observation).WithMany(p => p.OrganizationRelations)
                .HasForeignKey(d => d.ObservationId)
                .HasConstraintName("FK_dbo.OrganizationRelation_dbo.Observation_ObservationId");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationRelations)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_dbo.OrganizationRelation_dbo.Organization_OrganizationId");

            entity.HasOne(d => d.RelationType).WithMany(p => p.OrganizationRelations)
                .HasForeignKey(d => d.RelationTypeId)
                .HasConstraintName("FK_dbo.OrganizationRelation_dbo.OrganizationRelationType_RelationTypeId");
        });

        modelBuilder.Entity<OrganizationRelationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.OrganizationRelationType");

            entity.ToTable("OrganizationRelationType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<OrganizationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.OrganizationType");

            entity.ToTable("OrganizationType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<ProcessRecordResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ProcessRecordResult");

            entity.ToTable("ProcessRecordResult");

            entity.HasIndex(e => e.RejectedRecordId, "IX_RejectedRecord_Id");

            entity.HasIndex(e => e.NotificationCode, "IxNotificationCode");

            entity.HasIndex(e => e.ValidationCode, "IxValidationCode");

            entity.Property(e => e.ErrorContext).HasMaxLength(400);
            entity.Property(e => e.RejectedRecordId).HasColumnName("RejectedRecord_Id");

            entity.HasOne(d => d.RejectedRecord).WithMany(p => p.ProcessRecordResults)
                .HasForeignKey(d => d.RejectedRecordId)
                .HasConstraintName("FK_dbo.ProcessRecordResult_dbo.RejectedRecord_RejectedRecord_Id");
        });

        modelBuilder.Entity<ProcessSourceDataResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ProcessSourceDataResult");

            entity.ToTable("ProcessSourceDataResult");
        });

        modelBuilder.Entity<ProsessEngineHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.ProsessEngineHistory");

            entity.ToTable("ProsessEngineHistory");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.PublishDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<RecordNotificationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.RecordNotificationType");

            entity.ToTable("RecordNotificationType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Value).HasMaxLength(200);
        });

        modelBuilder.Entity<RecordValidationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.RecordValidationType");

            entity.ToTable("RecordValidationType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Value).HasMaxLength(200);
        });

        modelBuilder.Entity<RejectedRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.RejectedRecord");

            entity.ToTable("RejectedRecord");

            entity.HasIndex(e => e.RecordId, "IX_RecordId");

            entity.HasIndex(e => e.Gid, "Ix_Gid");

            entity.HasIndex(e => new { e.InstitutionCode, e.CollectionCode }, "Ix_InstitutionCode");

            entity.HasIndex(e => new { e.SourceDataId, e.DatetimeRecordProsessed }, "Ix_SourceDataId").IsDescending(false, true);

            entity.HasIndex(e => e.RecordId, "NonClusteredIndex-20171028-195228");

            entity.Property(e => e.CollectionCode).HasMaxLength(200);
            entity.Property(e => e.Gid).HasMaxLength(400);
            entity.Property(e => e.InstitutionCode).HasMaxLength(100);
            entity.Property(e => e.RecordId).HasMaxLength(400);
            entity.Property(e => e.TentativId).HasMaxLength(400);
        });

        modelBuilder.Entity<SensitiveObservationDatum>(entity =>
        {
            entity.HasKey(e => e.ObservationId).HasName("PK_dbo.SensitiveObservationData");

            entity.HasIndex(e => e.ObservationId, "IX_ObservationId");

            entity.Property(e => e.ObservationId).ValueGeneratedNever();
            entity.Property(e => e.Locality).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(3500);

            entity.HasOne(d => d.Observation).WithOne(p => p.SensitiveObservationDatum)
                .HasForeignKey<SensitiveObservationDatum>(d => d.ObservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_dbo.SensitiveObservationData_dbo.Observation_ObservationId");
        });

        modelBuilder.Entity<SpatialRefSy>(entity =>
        {
            entity.HasKey(e => e.Srid).HasName("PK__spatial___36B11BD5A2397CD6");

            entity.ToTable("spatial_ref_sys");

            entity.Property(e => e.Srid)
                .ValueGeneratedNever()
                .HasColumnName("srid");
            entity.Property(e => e.AuthName)
                .HasMaxLength(256)
                .IsUnicode(false)
                .HasColumnName("auth_name");
            entity.Property(e => e.AuthSrid).HasColumnName("auth_srid");
            entity.Property(e => e.Proj4text)
                .HasMaxLength(2048)
                .IsUnicode(false)
                .HasColumnName("proj4text");
            entity.Property(e => e.Srtext)
                .HasMaxLength(2048)
                .IsUnicode(false)
                .HasColumnName("srtext");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Tag");

            entity.ToTable("Tag");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Taxon>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.Taxon");

            entity.ToTable("Taxon");

            entity.HasIndex(e => e.PrefferedPopularname, "IX_PrefferedPopularname");

            entity.HasIndex(e => e.TaxonGroupId, "IX_TaxonGroupId");

            entity.HasIndex(e => e.TaxonRankId, "IX_TaxonRankId");

            entity.HasIndex(e => new { e.TaxonRankId, e.TaxonIdHiarchy }, "IX_TaxonRank_TaxonHirachy");

            entity.HasIndex(e => e.ValidScientificName, "IX_ValidScientificName");

            entity.HasIndex(e => e.ParentTaxonId, "IxParentTaxonId");

            entity.HasIndex(e => e.ParentTaxonId, "IxParentTaxonId_Id_ObsCount");

            entity.HasIndex(e => e.PrefferedPopularname, "NonClusteredIndex-20190129-153041");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateTimeUpdated).HasColumnType("datetime");
            entity.Property(e => e.PrefferedPopularname).HasMaxLength(100);
            entity.Property(e => e.ScientificNameIdHiarchy).HasMaxLength(200);
            entity.Property(e => e.TaxonIdHiarchy).HasMaxLength(200);
            entity.Property(e => e.ValidScientificName).HasMaxLength(100);
            entity.Property(e => e.ValidScientificNameAuthorship).HasMaxLength(200);

            entity.HasOne(d => d.TaxonGroup).WithMany(p => p.Taxons)
                .HasForeignKey(d => d.TaxonGroupId)
                .HasConstraintName("FK_dbo.Taxon_dbo.TaxonGroup_TaxonGroupId");

            entity.HasOne(d => d.TaxonRank).WithMany(p => p.Taxons)
                .HasForeignKey(d => d.TaxonRankId)
                .HasConstraintName("FK_dbo.Taxon_dbo.TaxonRank_TaxonRankId");
        });

        modelBuilder.Entity<TaxonGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonGroup");

            entity.ToTable("TaxonGroup");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(75);
        });

        modelBuilder.Entity<TaxonName>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonName");

            entity.ToTable("TaxonName");

            entity.HasIndex(e => e.ScientificName, "IX_ScientificName");

            entity.HasIndex(e => e.TaxonId, "IX_Taxon_Id");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateTimeUpdated).HasColumnType("datetime");
            entity.Property(e => e.ScientificName).HasMaxLength(100);
            entity.Property(e => e.ScientificNameAuthorship).HasMaxLength(200);
            entity.Property(e => e.TaxonId).HasColumnName("Taxon_Id");

            entity.HasOne(d => d.Taxon).WithMany(p => p.TaxonNames)
                .HasForeignKey(d => d.TaxonId)
                .HasConstraintName("FK_dbo.TaxonName_dbo.Taxon_Taxon_Id");
        });

        modelBuilder.Entity<TaxonPopularName>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonPopularName");

            entity.ToTable("TaxonPopularName");

            entity.HasIndex(e => e.TaxonId, "IX_Taxon_Id");

            entity.Property(e => e.DateTimeUpdated).HasColumnType("datetime");
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.TaxonId).HasColumnName("Taxon_Id");

            entity.HasOne(d => d.Taxon).WithMany(p => p.TaxonPopularNames)
                .HasForeignKey(d => d.TaxonId)
                .HasConstraintName("FK_dbo.TaxonPopularName_dbo.Taxon_Taxon_Id");
        });

        modelBuilder.Entity<TaxonProperty>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonProperty");

            entity.ToTable("TaxonProperty");

            entity.HasIndex(e => e.TaxonId, "IX_Taxon_Id");

            entity.Property(e => e.Context).HasMaxLength(50);
            entity.Property(e => e.DateTimeUpdated).HasColumnType("datetime");
            entity.Property(e => e.Prefix).HasMaxLength(50);
            entity.Property(e => e.ScientificName).HasMaxLength(100);
            entity.Property(e => e.Tag).HasMaxLength(50);
            entity.Property(e => e.TagGroup).HasMaxLength(50);
            entity.Property(e => e.TaxonId).HasColumnName("Taxon_Id");
            entity.Property(e => e.Url).HasMaxLength(500);

            entity.HasOne(d => d.Taxon).WithMany(p => p.TaxonProperties)
                .HasForeignKey(d => d.TaxonId)
                .HasConstraintName("FK_dbo.TaxonProperty_dbo.Taxon_Taxon_Id");
        });

        modelBuilder.Entity<TaxonRank>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonRank");

            entity.ToTable("TaxonRank");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(25);
        });

        modelBuilder.Entity<TaxonomyState>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.TaxonomyState");

            entity.ToTable("TaxonomyState");

            entity.Property(e => e.LastEventProcessedTimeStamp).HasColumnType("datetime");
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

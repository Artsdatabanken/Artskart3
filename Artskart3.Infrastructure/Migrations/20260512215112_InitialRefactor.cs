using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the PK dynamically since the auto-generated name differs between SQL Server instances
            migrationBuilder.Sql(@"
DECLARE @pkName nvarchar(max);
SELECT @pkName = QUOTENAME([kc].[name])
FROM [sys].[key_constraints] [kc]
WHERE [kc].[parent_object_id] = OBJECT_ID(N'[spatial_ref_sys]') AND [kc].[type] = 'PK';
IF @pkName IS NOT NULL EXEC(N'ALTER TABLE [spatial_ref_sys] DROP CONSTRAINT ' + @pkName);");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonRank]') AND name = 'CreatedAt') ALTER TABLE [TaxonRank] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonRank]') AND name = 'DeletedAt') ALTER TABLE [TaxonRank] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonRank]') AND name = 'IsDeleted') ALTER TABLE [TaxonRank] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonRank]') AND name = 'UpdatedAt') ALTER TABLE [TaxonRank] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonProperty]') AND name = 'CreatedAt') ALTER TABLE [TaxonProperty] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonProperty]') AND name = 'DeletedAt') ALTER TABLE [TaxonProperty] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonPopularName]') AND name = 'CreatedAt') ALTER TABLE [TaxonPopularName] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonPopularName]') AND name = 'DeletedAt') ALTER TABLE [TaxonPopularName] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonomyState]') AND name = 'CreatedAt') ALTER TABLE [TaxonomyState] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonomyState]') AND name = 'DeletedAt') ALTER TABLE [TaxonomyState] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonomyState]') AND name = 'IsDeleted') ALTER TABLE [TaxonomyState] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonomyState]') AND name = 'UpdatedAt') ALTER TABLE [TaxonomyState] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonName]') AND name = 'CreatedAt') ALTER TABLE [TaxonName] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonName]') AND name = 'DeletedAt') ALTER TABLE [TaxonName] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonGroup]') AND name = 'CreatedAt') ALTER TABLE [TaxonGroup] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonGroup]') AND name = 'DeletedAt') ALTER TABLE [TaxonGroup] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TaxonGroup]') AND name = 'UpdatedAt') ALTER TABLE [TaxonGroup] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Taxon]') AND name = 'CreatedAt') ALTER TABLE [Taxon] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Taxon]') AND name = 'DeletedAt') ALTER TABLE [Taxon] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tag]') AND name = 'CreatedAt') ALTER TABLE [Tag] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tag]') AND name = 'DeletedAt') ALTER TABLE [Tag] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tag]') AND name = 'IsDeleted') ALTER TABLE [Tag] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tag]') AND name = 'UpdatedAt') ALTER TABLE [Tag] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RejectedRecord]') AND name = 'CreatedAt') ALTER TABLE [RejectedRecord] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RejectedRecord]') AND name = 'DeletedAt') ALTER TABLE [RejectedRecord] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RejectedRecord]') AND name = 'IsDeleted') ALTER TABLE [RejectedRecord] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RejectedRecord]') AND name = 'UpdatedAt') ALTER TABLE [RejectedRecord] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordValidationType]') AND name = 'CreatedAt') ALTER TABLE [RecordValidationType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordValidationType]') AND name = 'DeletedAt') ALTER TABLE [RecordValidationType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordValidationType]') AND name = 'IsDeleted') ALTER TABLE [RecordValidationType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordValidationType]') AND name = 'UpdatedAt') ALTER TABLE [RecordValidationType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordNotificationType]') AND name = 'CreatedAt') ALTER TABLE [RecordNotificationType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordNotificationType]') AND name = 'DeletedAt') ALTER TABLE [RecordNotificationType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordNotificationType]') AND name = 'IsDeleted') ALTER TABLE [RecordNotificationType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[RecordNotificationType]') AND name = 'UpdatedAt') ALTER TABLE [RecordNotificationType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProsessEngineHistory]') AND name = 'CreatedAt') ALTER TABLE [ProsessEngineHistory] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProsessEngineHistory]') AND name = 'DeletedAt') ALTER TABLE [ProsessEngineHistory] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProsessEngineHistory]') AND name = 'IsDeleted') ALTER TABLE [ProsessEngineHistory] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProsessEngineHistory]') AND name = 'UpdatedAt') ALTER TABLE [ProsessEngineHistory] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessSourceDataResult]') AND name = 'CreatedAt') ALTER TABLE [ProcessSourceDataResult] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessSourceDataResult]') AND name = 'DeletedAt') ALTER TABLE [ProcessSourceDataResult] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessSourceDataResult]') AND name = 'IsDeleted') ALTER TABLE [ProcessSourceDataResult] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessSourceDataResult]') AND name = 'UpdatedAt') ALTER TABLE [ProcessSourceDataResult] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessRecordResult]') AND name = 'CreatedAt') ALTER TABLE [ProcessRecordResult] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessRecordResult]') AND name = 'DeletedAt') ALTER TABLE [ProcessRecordResult] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessRecordResult]') AND name = 'IsDeleted') ALTER TABLE [ProcessRecordResult] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessRecordResult]') AND name = 'UpdatedAt') ALTER TABLE [ProcessRecordResult] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationType]') AND name = 'CreatedAt') ALTER TABLE [OrganizationType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationType]') AND name = 'DeletedAt') ALTER TABLE [OrganizationType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationType]') AND name = 'IsDeleted') ALTER TABLE [OrganizationType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationType]') AND name = 'UpdatedAt') ALTER TABLE [OrganizationType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelationType]') AND name = 'CreatedAt') ALTER TABLE [OrganizationRelationType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelationType]') AND name = 'DeletedAt') ALTER TABLE [OrganizationRelationType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelationType]') AND name = 'IsDeleted') ALTER TABLE [OrganizationRelationType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelationType]') AND name = 'UpdatedAt') ALTER TABLE [OrganizationRelationType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelation]') AND name = 'CreatedAt') ALTER TABLE [OrganizationRelation] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelation]') AND name = 'DeletedAt') ALTER TABLE [OrganizationRelation] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelation]') AND name = 'IsDeleted') ALTER TABLE [OrganizationRelation] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[OrganizationRelation]') AND name = 'UpdatedAt') ALTER TABLE [OrganizationRelation] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Organization]') AND name = 'CreatedAt') ALTER TABLE [Organization] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Organization]') AND name = 'DeletedAt') ALTER TABLE [Organization] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Organization]') AND name = 'IsDeleted') ALTER TABLE [Organization] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Organization]') AND name = 'UpdatedAt') ALTER TABLE [Organization] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationQualityType]') AND name = 'CreatedAt') ALTER TABLE [ObservationQualityType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationQualityType]') AND name = 'DeletedAt') ALTER TABLE [ObservationQualityType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationQualityType]') AND name = 'IsDeleted') ALTER TABLE [ObservationQualityType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationQualityType]') AND name = 'UpdatedAt') ALTER TABLE [ObservationQualityType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationLink]') AND name = 'CreatedAt') ALTER TABLE [ObservationLink] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationLink]') AND name = 'DeletedAt') ALTER TABLE [ObservationLink] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationLink]') AND name = 'IsDeleted') ALTER TABLE [ObservationLink] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationLink]') AND name = 'UpdatedAt') ALTER TABLE [ObservationLink] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationError]') AND name = 'CreatedAt') ALTER TABLE [ObservationError] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationError]') AND name = 'DeletedAt') ALTER TABLE [ObservationError] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationError]') AND name = 'IsDeleted') ALTER TABLE [ObservationError] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationError]') AND name = 'UpdatedAt') ALTER TABLE [ObservationError] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            // Run outside the migration transaction to minimise lock contention on large tables
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationDetails]') AND name = 'CreatedAt') ALTER TABLE [ObservationDetails] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationDetails]') AND name = 'DeletedAt') ALTER TABLE [ObservationDetails] ADD [DeletedAt] datetime2 NULL;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationDetails]') AND name = 'IsDeleted') ALTER TABLE [ObservationDetails] ADD [IsDeleted] bit NOT NULL DEFAULT 0;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ObservationDetails]') AND name = 'UpdatedAt') ALTER TABLE [ObservationDetails] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            // Run outside the migration transaction to minimise lock contention on large tables
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Observation]') AND name = 'CreatedAt') ALTER TABLE [Observation] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Observation]') AND name = 'DeletedAt') ALTER TABLE [Observation] ADD [DeletedAt] datetime2 NULL;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Observation]') AND name = 'IsDeleted') ALTER TABLE [Observation] ADD [IsDeleted] bit NOT NULL DEFAULT 0;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Observation]') AND name = 'UpdatedAt') ALTER TABLE [Observation] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);


            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFileType]') AND name = 'CreatedAt') ALTER TABLE [MediaFileType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFileType]') AND name = 'DeletedAt') ALTER TABLE [MediaFileType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFileType]') AND name = 'IsDeleted') ALTER TABLE [MediaFileType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFileType]') AND name = 'UpdatedAt') ALTER TABLE [MediaFileType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFile]') AND name = 'CreatedAt') ALTER TABLE [MediaFile] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFile]') AND name = 'DeletedAt') ALTER TABLE [MediaFile] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFile]') AND name = 'IsDeleted') ALTER TABLE [MediaFile] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[MediaFile]') AND name = 'UpdatedAt') ALTER TABLE [MediaFile] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Location]') AND name = 'CreatedAt') ALTER TABLE [Location] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Location]') AND name = 'DeletedAt') ALTER TABLE [Location] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Location]') AND name = 'IsDeleted') ALTER TABLE [Location] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Location]') AND name = 'UpdatedAt') ALTER TABLE [Location] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportState]') AND name = 'CreatedAt') ALTER TABLE [ImportState] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportState]') AND name = 'DeletedAt') ALTER TABLE [ImportState] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportState]') AND name = 'IsDeleted') ALTER TABLE [ImportState] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportState]') AND name = 'UpdatedAt') ALTER TABLE [ImportState] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportNotification]') AND name = 'CreatedAt') ALTER TABLE [ImportNotification] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportNotification]') AND name = 'DeletedAt') ALTER TABLE [ImportNotification] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportNotification]') AND name = 'IsDeleted') ALTER TABLE [ImportNotification] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportNotification]') AND name = 'UpdatedAt') ALTER TABLE [ImportNotification] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportLog]') AND name = 'CreatedAt') ALTER TABLE [ImportLog] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportLog]') AND name = 'DeletedAt') ALTER TABLE [ImportLog] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportLog]') AND name = 'IsDeleted') ALTER TABLE [ImportLog] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ImportLog]') AND name = 'UpdatedAt') ALTER TABLE [ImportLog] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[geometry_columns]') AND name = 'CreatedAt') ALTER TABLE [geometry_columns] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[geometry_columns]') AND name = 'DeletedAt') ALTER TABLE [geometry_columns] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[geometry_columns]') AND name = 'Id') ALTER TABLE [geometry_columns] ADD [Id] int NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[geometry_columns]') AND name = 'IsDeleted') ALTER TABLE [geometry_columns] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[geometry_columns]') AND name = 'UpdatedAt') ALTER TABLE [geometry_columns] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.Filter",
                table: "Filter");

            migrationBuilder.Sql(@"
                DECLARE @var nvarchar(max);
                SELECT @var = QUOTENAME([d].[name])
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Filter]') AND [c].[name] = N'Id');
                IF @var IS NOT NULL EXEC(N'ALTER TABLE [Filter] DROP CONSTRAINT ' + @var + ';');
                ALTER TABLE [Filter] DROP COLUMN [Id];
                ALTER TABLE [Filter] ADD [Id] int NOT NULL IDENTITY(1,1);
            ");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Filter",
                table: "Filter",
                column: "Id");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Filter]') AND name = 'CreatedAt') ALTER TABLE [Filter] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Filter]') AND name = 'DeletedAt') ALTER TABLE [Filter] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Filter]') AND name = 'IsDeleted') ALTER TABLE [Filter] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Filter]') AND name = 'UpdatedAt') ALTER TABLE [Filter] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[FAB4Exclude]') AND name = 'CreatedAt') ALTER TABLE [FAB4Exclude] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[FAB4Exclude]') AND name = 'DeletedAt') ALTER TABLE [FAB4Exclude] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[FAB4Exclude]') AND name = 'IsDeleted') ALTER TABLE [FAB4Exclude] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[FAB4Exclude]') AND name = 'UpdatedAt') ALTER TABLE [FAB4Exclude] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.ExportStatus",
                table: "ExportStatus");

            migrationBuilder.Sql(@"
                DECLARE @var nvarchar(max);
                SELECT @var = QUOTENAME([d].[name])
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ExportStatus]') AND [c].[name] = N'Id');
                IF @var IS NOT NULL EXEC(N'ALTER TABLE [ExportStatus] DROP CONSTRAINT ' + @var + ';');
                ALTER TABLE [ExportStatus] DROP COLUMN [Id];
                ALTER TABLE [ExportStatus] ADD [Id] int NOT NULL IDENTITY(1,1);
            ");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExportStatus",
                table: "ExportStatus",
                column: "Id");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ExportStatus]') AND name = 'CreatedAt') ALTER TABLE [ExportStatus] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ExportStatus]') AND name = 'DeletedAt') ALTER TABLE [ExportStatus] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ExportStatus]') AND name = 'IsDeleted') ALTER TABLE [ExportStatus] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ExportStatus]') AND name = 'UpdatedAt') ALTER TABLE [ExportStatus] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeletedItem]') AND name = 'CreatedAt') ALTER TABLE [DeletedItem] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeletedItem]') AND name = 'DeletedAt') ALTER TABLE [DeletedItem] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeletedItem]') AND name = 'IsDeleted') ALTER TABLE [DeletedItem] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeletedItem]') AND name = 'UpdatedAt') ALTER TABLE [DeletedItem] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CommandLog]') AND name = 'CreatedAt') ALTER TABLE [CommandLog] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CommandLog]') AND name = 'DeletedAt') ALTER TABLE [CommandLog] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CommandLog]') AND name = 'IsDeleted') ALTER TABLE [CommandLog] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CommandLog]') AND name = 'UpdatedAt') ALTER TABLE [CommandLog] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CategoryType]') AND name = 'CreatedAt') ALTER TABLE [CategoryType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CategoryType]') AND name = 'DeletedAt') ALTER TABLE [CategoryType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CategoryType]') AND name = 'IsDeleted') ALTER TABLE [CategoryType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[CategoryType]') AND name = 'UpdatedAt') ALTER TABLE [CategoryType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Category]') AND name = 'CreatedAt') ALTER TABLE [Category] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Category]') AND name = 'DeletedAt') ALTER TABLE [Category] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Category]') AND name = 'IsDeleted') ALTER TABLE [Category] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Category]') AND name = 'UpdatedAt') ALTER TABLE [Category] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Behavior]') AND name = 'CreatedAt') ALTER TABLE [Behavior] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Behavior]') AND name = 'DeletedAt') ALTER TABLE [Behavior] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Behavior]') AND name = 'IsDeleted') ALTER TABLE [Behavior] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Behavior]') AND name = 'UpdatedAt') ALTER TABLE [Behavior] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[BasisOfRecord]') AND name = 'CreatedAt') ALTER TABLE [BasisOfRecord] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[BasisOfRecord]') AND name = 'DeletedAt') ALTER TABLE [BasisOfRecord] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[BasisOfRecord]') AND name = 'UpdatedAt') ALTER TABLE [BasisOfRecord] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AreaType]') AND name = 'CreatedAt') ALTER TABLE [AreaType] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AreaType]') AND name = 'DeletedAt') ALTER TABLE [AreaType] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AreaType]') AND name = 'IsDeleted') ALTER TABLE [AreaType] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AreaType]') AND name = 'UpdatedAt') ALTER TABLE [AreaType] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_AreaTypeID] ON [Area];");
            migrationBuilder.Sql("ALTER TABLE [Area] DROP COLUMN [Centroid];");

            migrationBuilder.AlterColumn<Geometry>(
                name: "WktPolygon",
                table: "Area",
                type: "geometry",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geometry");

            migrationBuilder.Sql("ALTER TABLE [Area] ADD [Centroid] AS ([WktPolygon].[STCentroid]());");

            migrationBuilder.CreateIndex(
                name: "IX_AreaTypeID",
                table: "Area",
                columns: new[] { "AreaTypeID", "Id", "Fid" });;

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Area]') AND name = 'CreatedAt') ALTER TABLE [Area] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Area]') AND name = 'DeletedAt') ALTER TABLE [Area] ADD [DeletedAt] datetime2 NULL;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Area]') AND name = 'IsDeleted') ALTER TABLE [Area] ADD [IsDeleted] bit NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Area]') AND name = 'UpdatedAt') ALTER TABLE [Area] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';");

            migrationBuilder.AddPrimaryKey(
                name: "PK__spatial___36B11BD5A2397CD6",
                table: "spatial_ref_sys",
                column: "srid");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation",
                column: "DateLastModified");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation",
                column: "InstitutionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId",
                table: "Observation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation",
                columns: new[] { "LocationId", "HasErrors", "HasAnnotations" });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation",
                column: "MonthCollected");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation",
                column: "TaxonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation",
                column: "YearCollected");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation",
                columns: new[] { "YearCollected", "LocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK__spatial___36B11BD5A2397CD6",
                table: "spatial_ref_sys");

            migrationBuilder.DropIndex(
                name: "IX_Observation_CategoryId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_DateLastModified",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionCode",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_InstitutionId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_LocationId_HasErrors_HasAnnotations",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_MonthCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_TaxonGroupId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_YearCollected_LocationId",
                table: "Observation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonRank");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonRank");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TaxonRank");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TaxonRank");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonProperty");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonProperty");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonPopularName");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonPopularName");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonomyState");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonomyState");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TaxonomyState");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TaxonomyState");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonName");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonName");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TaxonGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaxonGroup");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TaxonGroup");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Taxon");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Taxon");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RejectedRecord");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RejectedRecord");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RejectedRecord");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RejectedRecord");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RecordValidationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RecordValidationType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RecordValidationType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecordValidationType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RecordNotificationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RecordNotificationType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RecordNotificationType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecordNotificationType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProsessEngineHistory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProsessEngineHistory");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProsessEngineHistory");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProsessEngineHistory");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProcessSourceDataResult");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProcessSourceDataResult");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProcessSourceDataResult");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProcessSourceDataResult");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProcessRecordResult");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProcessRecordResult");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProcessRecordResult");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProcessRecordResult");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OrganizationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "OrganizationType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrganizationType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OrganizationType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OrganizationRelationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "OrganizationRelationType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrganizationRelationType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OrganizationRelationType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OrganizationRelation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "OrganizationRelation");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrganizationRelation");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OrganizationRelation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ObservationQualityType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ObservationQualityType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ObservationQualityType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ObservationQualityType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ObservationLink");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ObservationLink");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ObservationLink");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ObservationLink");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ObservationError");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ObservationError");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ObservationError");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ObservationError");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ObservationDetails");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ObservationDetails");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ObservationDetails");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ObservationDetails");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Observation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Observation");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Observation");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Observation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MediaFileType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MediaFileType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MediaFileType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MediaFileType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ImportState");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ImportState");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ImportState");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ImportState");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ImportNotification");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ImportNotification");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ImportNotification");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ImportNotification");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ImportLog");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ImportLog");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ImportLog");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ImportLog");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "geometry_columns");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "geometry_columns");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "geometry_columns");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "geometry_columns");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "geometry_columns");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Filter");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Filter");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Filter");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Filter");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FAB4Exclude");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "FAB4Exclude");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "FAB4Exclude");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FAB4Exclude");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ExportStatus");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ExportStatus");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ExportStatus");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ExportStatus");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DeletedItem");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DeletedItem");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DeletedItem");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DeletedItem");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CommandLog");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CommandLog");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommandLog");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CommandLog");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CategoryType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CategoryType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CategoryType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CategoryType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Behavior");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Behavior");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Behavior");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Behavior");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BasisOfRecord");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BasisOfRecord");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BasisOfRecord");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AreaType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AreaType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AreaType");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AreaType");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Area");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "BasisOfRecord",
                newName: "Deleted");

            migrationBuilder.AddColumn<Geometry>(
                name: "Geometry",
                table: "SensitiveObservationData",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "SHAPE",
                table: "Maskeringsruter_8x8km",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "SHAPE",
                table: "Maskeringsruter_4x4km",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "SHAPE",
                table: "Maskeringsruter_16x16km",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "Geometry",
                table: "Location",
                type: "geometry",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Filter",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newsequentialid())",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValueSql: "(newsequentialid())");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ExportStatus",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Geometry>(
                name: "WktPolygon",
                table: "Area",
                type: "geometry",
                nullable: false,
                oldClrType: typeof(Geometry),
                oldType: "geometry",
                oldNullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "WktPolygonGm",
                table: "Area",
                type: "geometry",
                nullable: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK__spatial___36B11BD521FD7814",
                table: "spatial_ref_sys",
                column: "srid");

            migrationBuilder.CreateTable(
                name: "TempIndexNode1Time2601052127",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode1Time2601052127", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode1Time2601052134",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode1Time2601052134", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode40Time2601131542",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode40Time2601131542", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode40Time2601151351",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode40Time2601151351", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode40Time2601190922",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode40Time2601190922", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode40Time2601191055",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode40Time2601191055", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode40Time2601241602",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode40Time2601241602", x => x.ProxyId);
                });

            migrationBuilder.CreateTable(
                name: "TempIndexNode8Time2601052207",
                columns: table => new
                {
                    ProxyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempIndexNode8Time2601052207", x => x.ProxyId);
                });

            migrationBuilder.CreateIndex(
                name: "qgs_SHAPE_sidx",
                table: "Maskeringsruter_8x8km",
                column: "SHAPE");

            migrationBuilder.CreateIndex(
                name: "SpatialIndex-WktPolygon",
                table: "Maskeringsruter_8x8km",
                column: "SHAPE");

            migrationBuilder.CreateIndex(
                name: "qgs_SHAPE_sidx",
                table: "Maskeringsruter_4x4km",
                column: "SHAPE");

            migrationBuilder.CreateIndex(
                name: "qgs_SHAPE_sidx",
                table: "Maskeringsruter_16x16km",
                column: "SHAPE");

            migrationBuilder.CreateIndex(
                name: "SpatialIndex-WktPolygon",
                table: "Maskeringsruter_16x16km",
                column: "SHAPE");

            migrationBuilder.CreateIndex(
                name: "SpatialIndex-Geometry",
                table: "Location",
                column: "Geometry");

            migrationBuilder.CreateIndex(
                name: "IX_EastNorthGeom",
                table: "Location",
                column: "East");

            migrationBuilder.CreateIndex(
                name: "SpatialIndex-WktPolygon",
                table: "Area",
                column: "WktPolygon");
        }
    }
}

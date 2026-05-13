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

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonRank",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonRank",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TaxonRank",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonRank",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonProperty",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonProperty",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonPopularName",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonPopularName",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonomyState",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonomyState",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TaxonomyState",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonomyState",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonName",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonName",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TaxonGroup",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaxonGroup",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonGroup",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Taxon",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Taxon",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Tag",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Tag",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tag",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Tag",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RejectedRecord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RejectedRecord",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RejectedRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RejectedRecord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RecordValidationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RecordValidationType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RecordValidationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecordValidationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RecordNotificationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RecordNotificationType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RecordNotificationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecordNotificationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProsessEngineHistory",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProsessEngineHistory",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProsessEngineHistory",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProsessEngineHistory",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProcessSourceDataResult",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProcessSourceDataResult",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProcessSourceDataResult",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProcessSourceDataResult",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProcessRecordResult",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProcessRecordResult",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProcessRecordResult",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProcessRecordResult",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OrganizationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "OrganizationType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrganizationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrganizationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OrganizationRelationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "OrganizationRelationType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrganizationRelationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrganizationRelationType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OrganizationRelation",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "OrganizationRelation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrganizationRelation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrganizationRelation",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Organization",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Organization",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Organization",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Organization",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ObservationQualityType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ObservationQualityType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ObservationQualityType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ObservationQualityType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ObservationLink",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ObservationLink",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ObservationLink",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ObservationLink",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ObservationError",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ObservationError",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ObservationError",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ObservationError",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Run outside the migration transaction to minimise lock contention on large tables
            migrationBuilder.Sql(
                "ALTER TABLE [ObservationDetails] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [ObservationDetails] ADD [DeletedAt] datetime2 NULL;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [ObservationDetails] ADD [IsDeleted] bit NOT NULL DEFAULT 0;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [ObservationDetails] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            // Run outside the migration transaction to minimise lock contention on large tables
            migrationBuilder.Sql(
                "ALTER TABLE [Observation] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [Observation] ADD [DeletedAt] datetime2 NULL;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [Observation] ADD [IsDeleted] bit NOT NULL DEFAULT 0;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "ALTER TABLE [Observation] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';",
                suppressTransaction: true);


            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MediaFileType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "MediaFileType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MediaFileType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MediaFileType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MediaFile",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "MediaFile",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MediaFile",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MediaFile",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Location",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Location",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Location",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Location",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ImportState",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ImportState",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ImportState",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ImportState",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ImportNotification",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ImportNotification",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ImportNotification",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ImportNotification",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ImportLog",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ImportLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ImportLog",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ImportLog",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "geometry_columns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "geometry_columns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "geometry_columns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "geometry_columns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "geometry_columns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Filter",
                type: "int",
                nullable: false,
                defaultValueSql: "(newsequentialid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newsequentialid())");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Filter",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Filter",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Filter",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Filter",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FAB4Exclude",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "FAB4Exclude",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "FAB4Exclude",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FAB4Exclude",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "ExportStatus",
                type: "int",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ExportStatus",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ExportStatus",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ExportStatus",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ExportStatus",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DeletedItem",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "DeletedItem",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DeletedItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DeletedItem",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CommandLog",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CommandLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommandLog",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CommandLog",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CategoryType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CategoryType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CategoryType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CategoryType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Category",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Category",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Category",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Category",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Behavior",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Behavior",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Behavior",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Behavior",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "BasisOfRecord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "BasisOfRecord",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BasisOfRecord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AreaType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AreaType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AreaType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AreaType",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Geometry>(
                name: "WktPolygon",
                table: "Area",
                type: "geometry",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geometry");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Area",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Area",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Area",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Area",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

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

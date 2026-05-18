using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BasisOfRecord hadde en legacy 'Deleted'-kolonne som aldri ble omdøpt til 'IsDeleted'
            // av InitialRefactor-migrasjonen; retter dette nå.
            migrationBuilder.RenameColumn(
                name: "Deleted",
                table: "BasisOfRecord",
                newName: "IsDeleted");

            // Vi hadde både 'Deleted' og 'IsDeleted' kolonne i de tre tabellene under. Setter IsDeleted til verdien som Deleted hadde før Deleted slettes.
            migrationBuilder.Sql("UPDATE [Behavior] SET [IsDeleted] = 1 WHERE [Deleted] = 1 AND [IsDeleted] = 0;");
            migrationBuilder.Sql("UPDATE [RecordNotificationType] SET [IsDeleted] = 1 WHERE [Deleted] = 1 AND [IsDeleted] = 0;");
            migrationBuilder.Sql("UPDATE [RecordValidationType] SET [IsDeleted] = 1 WHERE [Deleted] = 1 AND [IsDeleted] = 0;");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "RecordValidationType");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "RecordNotificationType");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "Behavior");

            // renamer kolonner for å unngå HasColumnName - sparer oss for hodebry når vi skal kjøre sql spørringer siden.
            migrationBuilder.RenameColumn(
                name: "Taxon_Id",
                table: "TaxonProperty",
                newName: "TaxonId");

            migrationBuilder.RenameColumn(
                name: "DateTimeUpdated",
                table: "TaxonProperty",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Taxon_Id",
                table: "TaxonPopularName",
                newName: "TaxonId");

            migrationBuilder.RenameColumn(
                name: "Preffered",
                table: "TaxonPopularName",
                newName: "Preferred");

            migrationBuilder.RenameColumn(
                name: "DateTimeUpdated",
                table: "TaxonPopularName",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Taxon_Id",
                table: "TaxonName",
                newName: "TaxonId");

            migrationBuilder.RenameColumn(
                name: "DateTimeUpdated",
                table: "TaxonName",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "Deleted",
                table: "TaxonGroup",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "TaxonId",
                table: "Taxon",
                newName: "ExternalTaxonId");

            migrationBuilder.RenameColumn(
                name: "PrefferedPopularname",
                table: "Taxon",
                newName: "PreferredPopularName");

            migrationBuilder.RenameColumn(
                name: "DateTimeUpdated",
                table: "Taxon",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "OccurenceId",
                table: "Observation",
                newName: "OccurrenceId");

            migrationBuilder.RenameColumn(
                name: "DateTimeRecordProsessed",
                table: "Observation",
                newName: "DateTimeRecordProcessed");

            migrationBuilder.RenameColumn(
                name: "AreaTypeID",
                table: "Area",
                newName: "AreaTypeId");

            // endrer datetime kolonner til type datetime2 for konsistens.
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonProperty",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonPopularName",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "TaxonName",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Taxon",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeRecordProcessed",
                table: "Observation",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            // fjerner kolonner vi ikke trenger
            migrationBuilder.DropColumn(
                name: "GmBbox",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "WktPolygonGm",
                table: "Area");

            // Legger til geometri hvis den ikke eksisterer allerede
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SensitiveObservationData]') AND name = 'Geometry') ALTER TABLE [SensitiveObservationData] ADD [Geometry] geometry NULL;");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Location]') AND name = 'Geometry') ALTER TABLE [Location] ADD [Geometry] geometry NULL;");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Maskeringsruter_8x8km]') AND name = 'SHAPE') ALTER TABLE [Maskeringsruter_8x8km] ADD [SHAPE] geometry NULL;");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Maskeringsruter_4x4km]') AND name = 'SHAPE') ALTER TABLE [Maskeringsruter_4x4km] ADD [SHAPE] geometry NULL;");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Maskeringsruter_16x16km]') AND name = 'SHAPE') ALTER TABLE [Maskeringsruter_16x16km] ADD [SHAPE] geometry NULL;");

            // Slett views som ikke ble fjernet automatisk
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[View_Export_POLYGON];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[View_Export_POINT];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[View_Export];");

            // Slett TempIndexNode-tabeller som ikke ble fjernet autiomatisk
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode1Time2601052127];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode1Time2601052134];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode8Time2601052207];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode40Time2601131542];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode40Time2601151351];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode40Time2601190922];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode40Time2601191055];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[TempIndexNode40Time2601241602];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "TaxonProperty",
                newName: "DateTimeUpdated");

            migrationBuilder.RenameColumn(
                name: "TaxonId",
                table: "TaxonProperty",
                newName: "Taxon_Id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "TaxonPopularName",
                newName: "DateTimeUpdated");

            migrationBuilder.RenameColumn(
                name: "TaxonId",
                table: "TaxonPopularName",
                newName: "Taxon_Id");

            migrationBuilder.RenameColumn(
                name: "Preferred",
                table: "TaxonPopularName",
                newName: "Preffered");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "TaxonName",
                newName: "DateTimeUpdated");

            migrationBuilder.RenameColumn(
                name: "TaxonId",
                table: "TaxonName",
                newName: "Taxon_Id");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "TaxonGroup",
                newName: "Deleted");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Taxon",
                newName: "DateTimeUpdated");

            migrationBuilder.RenameColumn(
                name: "PreferredPopularName",
                table: "Taxon",
                newName: "PrefferedPopularname");

            migrationBuilder.RenameColumn(
                name: "ExternalTaxonId",
                table: "Taxon",
                newName: "TaxonId");

            migrationBuilder.RenameColumn(
                name: "OccurrenceId",
                table: "Observation",
                newName: "OccurenceId");

            migrationBuilder.RenameColumn(
                name: "DateTimeRecordProcessed",
                table: "Observation",
                newName: "DateTimeRecordProsessed");

            migrationBuilder.RenameColumn(
                name: "AreaTypeId",
                table: "Area",
                newName: "AreaTypeID");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeUpdated",
                table: "TaxonProperty",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeUpdated",
                table: "TaxonPopularName",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeUpdated",
                table: "TaxonName",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeUpdated",
                table: "Taxon",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "RecordValidationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "RecordNotificationType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateTimeRecordProsessed",
                table: "Observation",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "Behavior",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "BasisOfRecord",
                newName: "Deleted");

            migrationBuilder.DropColumn(
                name: "Geometry",
                table: "SensitiveObservationData");

            migrationBuilder.DropColumn(
                name: "Geometry",
                table: "Location");

            migrationBuilder.DropColumn(
                name: "SHAPE",
                table: "Maskeringsruter_8x8km");

            migrationBuilder.DropColumn(
                name: "SHAPE",
                table: "Maskeringsruter_4x4km");

            migrationBuilder.DropColumn(
                name: "SHAPE",
                table: "Maskeringsruter_16x16km");

            migrationBuilder.AddColumn<string>(
                name: "GmBbox",
                table: "Area",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Geometry>(
                name: "WktPolygonGm",
                table: "Area",
                type: "geometry",
                nullable: true);
        }
    }
}

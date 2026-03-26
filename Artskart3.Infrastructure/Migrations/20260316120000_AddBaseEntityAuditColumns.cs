using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseEntityAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add BaseEntity audit columns to all tables that inherit from BaseEntity
            // Checks for existence to maintain idempotency
            
            var tableName = "Area";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "AreaType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "BasisOfRecord";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Behavior";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Category";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "CategoryType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "CommandLog";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "DeletedItem";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ExportStatus";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "FAB4Exclude";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Filter";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ImportLog";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ImportNotification";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ImportState";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Location";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "MediaFile";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "MediaFileType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ObservationError";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ObservationLink";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ObservationQualityType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Organization";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "OrganizationRelation";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "OrganizationRelationType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "OrganizationType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ProcessRecordResult";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "ProcessSourceDataResult";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "RecordNotificationType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "RecordValidationType";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "RejectedRecord";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Tag";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "Taxon";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonGroup";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonName";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonPopularName";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonProperty";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonRank";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            tableName = "TaxonomyState";
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'CreatedAt') ALTER TABLE [{tableName}] ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'UpdatedAt') ALTER TABLE [{tableName}] ADD UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'IsDeleted') ALTER TABLE [{tableName}] ADD IsDeleted bit NOT NULL DEFAULT 0;");
            migrationBuilder.Sql($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'DeletedAt') ALTER TABLE [{tableName}] ADD DeletedAt datetime2 NULL;");

            // Note: Add more tables as needed
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrade not supported for this idempotent migration
            // Would need manual intervention to safely rollback
        }
    }
}

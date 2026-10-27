using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDataSourceAndOrganizationAliases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceDatasetId",
            table: "Organization",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DataSource",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RemoteDataSourceType = table.Column<int>(type: "int", nullable: false),
                RecordsBatchSize = table.Column<int>(type: "int", nullable: false),
                RemoteDataSourceV = table.Column<int>(type: "int", nullable: false),
                RemoteDataSourceAdress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                GbifIptArchiveName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                IsEditedNameOrNotes = table.Column<bool>(type: "bit", nullable: false),
                NonValidOccuranceIds = table.Column<bool>(type: "bit", nullable: false),
                GbifApiQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                HarvestPropertyOverrides = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ImportPropertyOverrides = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdateFrequencyInDays = table.Column<int>(type: "int", nullable: false),
                GbifDataSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GbifDoi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                GbifPublisherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsSensitive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataSource", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationAlias",
            columns: table => new
            {
                OrganizationId = table.Column<int>(type: "int", nullable: false),
                Alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationAlias", x => new { x.OrganizationId, x.Alias });
                table.ForeignKey(
                    name: "FK_OrganizationAlias_Organization",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Organization_SourceDatasetId",
            table: "Organization",
            columns: new[] { "ParentId", "SourceDatasetId" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationAlias_Alias",
            table: "OrganizationAlias",
            column: "Alias",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DataSource");

        migrationBuilder.DropTable(
            name: "OrganizationAlias");

        migrationBuilder.DropIndex(
            name: "IX_Organization_SourceDatasetId",
            table: "Organization");

        migrationBuilder.DropColumn(
            name: "SourceDatasetId",
            table: "Organization");
    }
}

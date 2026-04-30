using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSource",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProviderType = table.Column<int>(type: "int", nullable: false),
                    RecordsBatchSize = table.Column<int>(type: "int", nullable: false),
                    WebServiceVersion = table.Column<int>(type: "int", nullable: false),
                    RemoteAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ArchiveName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEditedNameOrNotes = table.Column<bool>(type: "bit", nullable: false),
                    NonValidOccurrenceIds = table.Column<bool>(type: "bit", nullable: false),
                    GbifApiQuery = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HarvestPropertyOverrides = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImportPropertyOverrides = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdateFrequencyInDays = table.Column<int>(type: "int", nullable: false),
                    Doi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GbifPublisherId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSource", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_BasisOfRecordId",
                table: "Observation",
                column: "BasisOfRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_CoordinatePrecisionInMeters",
                table: "Observation",
                column: "CoordinatePrecisionInMeters");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_TaxonGroupId_LocationId",
                table: "Observation",
                columns: new[] { "TaxonGroupId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSource_IsDeleted",
                table: "DataSource",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_DataSource_ProviderType",
                table: "DataSource",
                column: "ProviderType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSource");

            migrationBuilder.DropIndex(
                name: "IX_Observation_BasisOfRecordId",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_CoordinatePrecisionInMeters",
                table: "Observation");

            migrationBuilder.DropIndex(
                name: "IX_Observation_TaxonGroupId_LocationId",
                table: "Observation");
        }
    }
}

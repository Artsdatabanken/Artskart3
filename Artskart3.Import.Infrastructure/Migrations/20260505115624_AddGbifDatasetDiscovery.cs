using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Import.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGbifDatasetDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GbifDatasetDiscovery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GbifId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GbifPublisherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishingOrganizationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HomepageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DwcArchiveUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Doi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Identifiers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GbifDatasetDiscovery", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GbifDatasetDiscovery_GbifId",
                table: "GbifDatasetDiscovery",
                column: "GbifId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GbifDatasetDiscovery_Status",
                table: "GbifDatasetDiscovery",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GbifDatasetDiscovery");
        }
    }
}

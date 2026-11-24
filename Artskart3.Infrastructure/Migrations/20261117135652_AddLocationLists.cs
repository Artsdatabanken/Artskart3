using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLocationLists : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LocationListEntries",
            columns: table => new
            {
                ListId = table.Column<int>(type: "int", nullable: false),
                LocationId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocationListEntries", x => new { x.ListId, x.LocationId });
            });

        migrationBuilder.CreateTable(
            name: "LocationLists",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Hash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                LocationCount = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocationLists", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LocationLists_Hash",
            table: "LocationLists",
            column: "Hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LocationListEntries");

        migrationBuilder.DropTable(
            name: "LocationLists");
    }
}

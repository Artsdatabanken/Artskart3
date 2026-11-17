using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddHistoricalAreaLocation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HistoricalAreaLocation",
            columns: table => new
            {
                AreaId = table.Column<int>(type: "int", nullable: false),
                LocationId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HistoricalAreaLocation", x => new { x.AreaId, x.LocationId });
            });

        // EF kan ikke sette komprimering på en primærnøkkel. Tabellen er tom her, så ombyggingen tar ingen tid.
        migrationBuilder.Sql("ALTER TABLE dbo.HistoricalAreaLocation REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HistoricalAreaLocation");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeedMapLayerGrenser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "MapLayer",
            columns: ["Name", "Type", "Url", "Layers", "Format", "Version", "Attribution", "CreatedAt", "UpdatedAt", "IsDeleted"],
            values: new object[] { "Grenser", "WMS", "https://wms.geonorge.no/skwms1/wms.adm_enheter2?", "kommuner_gjel", "image/png", "1.3.0", "admGrenserAttribution", new DateTime(2026, 9, 14), new DateTime(2026, 9, 14), false });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "MapLayer",
            keyColumn: "Name",
            keyValue: "Grenser");
    }
}

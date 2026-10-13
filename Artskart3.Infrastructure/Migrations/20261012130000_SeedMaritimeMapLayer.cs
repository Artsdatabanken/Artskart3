using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Artskart3.Infrastructure.Migrations;

[DbContext(typeof(ArtskartDbContext))]
[Migration("20261012130000_SeedMaritimeMapLayer")]
public partial class SeedMaritimeMapLayer : Migration
{
    private static readonly DateTime SeededAt = new(2026, 10, 12);
    private const string LayerName = "MaritimeGrenser";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "MapLayer",
            columns: ["Name", "Type", "Url", "Layers", "Format", "Version", "Attribution", "CreatedAt", "UpdatedAt", "IsDeleted", "SortOrder"],
            columnTypes: ["nvarchar(200)", "nvarchar(20)", "nvarchar(1000)", "nvarchar(500)", "nvarchar(100)", "nvarchar(20)", "nvarchar(1000)", "datetime2", "datetime2", "bit", "int"],
            values: new object[]
            {
                LayerName,
                "WMS",
                "https://wms.geonorge.no/skwms1/wms.nmg?",
                "Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn",
                "image/png",
                "1.3.0",
                "Kartverket",
                SeededAt,
                SeededAt,
                false,
                60,
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [MapLayer]
            WHERE [Name] = N'MaritimeGrenser'
                AND [Url] = N'https://wms.geonorge.no/skwms1/wms.nmg?'
                AND [Layers] = N'Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn';
            """);
    }
}

using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ArtskartDbContext))]
[Migration("20260924000000_SeedBioklimatiskeMapLayers")]
public partial class SeedBioklimatiskeMapLayers : Migration
{
    private static readonly DateTime SeededAt = new(2026, 9, 24);

    private static readonly string[] LayerNames = ["Bioseksjoner", "Biosoner"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM [MapLayer] WHERE [Name] IN (N'Bioseksjoner', N'Biosoner');");

        migrationBuilder.InsertData(
            table: "MapLayer",
            columns: ["Name", "Type", "Url", "Layers", "Format", "Version", "Attribution", "CreatedAt", "UpdatedAt", "IsDeleted"],
            values: new object[,]
            {
                { "Bioseksjoner", "WMS", "https://kart.artsdatabanken.no/wms/lkm.aspx?", "seksjoner2017", "image/png", "1.3.0", "Artsdatabanken", SeededAt, SeededAt, false },
                { "Biosoner", "WMS", "https://kart.artsdatabanken.no/wms/lkm.aspx?", "soner2017", "image/png", "1.3.0", "Artsdatabanken", SeededAt, SeededAt, false },
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var name in LayerNames)
        {
            migrationBuilder.DeleteData(
                table: "MapLayer",
                keyColumn: "Name",
                keyValue: name);
        }
    }
}

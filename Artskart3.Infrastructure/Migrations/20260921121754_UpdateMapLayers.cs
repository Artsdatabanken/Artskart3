using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class UpdateMapLayers : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 9, 21);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in new[] { "admGrenser", "Vern" })
            {
                migrationBuilder.DeleteData(
                    table: "MapLayer",
                    keyColumn: "Name",
                    keyValue: name);
            }

            migrationBuilder.InsertData(
                table: "MapLayer",
                columns: new[] { "Name", "Type", "Url", "Layers", "Format", "Version", "Attribution", "CreatedAt", "UpdatedAt", "IsDeleted" },
                values: new object[,]
                {
                    { "admGrenser", "WMS", "https://wms.geonorge.no/skwms1/wms.adm_enheter2?", "kommuner_gjel,fylker_gjel", "image/png", "1.3.0", "Geonorge", SeededAt, SeededAt, false },
                    { "Vern", "WMS", "https://wms.miljodirektoratet.no/arcgis/services/vern/mapserver/WMSServer?", "naturvern_klasser_omrade,foreslatt_naturvern_omrade", "image/png", "1.3.0", "Miljodirektoratet", SeededAt, SeededAt, false },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in new[] { "admGrenser", "Vern" })
            {
                migrationBuilder.DeleteData(
                    table: "MapLayer",
                    keyColumn: "Name",
                    keyValue: name);
            }
        }
    }

using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Artskart3.Infrastructure.Migrations;

[DbContext(typeof(ArtskartDbContext))]
[Migration("20261006010000_UpdateAdministrativeBoundaryMapLayer")]
public partial class UpdateAdministrativeBoundaryMapLayer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "MapLayer",
            keyColumns: ["Name"],
            keyColumnTypes: ["nvarchar(200)"],
            keyValues: ["admGrenser"],
            columns: ["Url", "Layers", "Attribution", "UpdatedAt"],
            columnTypes: ["nvarchar(1000)", "nvarchar(500)", "nvarchar(1000)", "datetime2"],
            values: ["https://wms.geonorge.no/skwms1/wms.topo", "kd_administrative_grenser,adm_grenser", "Kartverket", new DateTime(2026, 10, 6)]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "MapLayer",
            keyColumns: ["Name"],
            keyColumnTypes: ["nvarchar(200)"],
            keyValues: ["admGrenser"],
            columns: ["Url", "Layers", "Attribution", "UpdatedAt"],
            columnTypes: ["nvarchar(1000)", "nvarchar(500)", "nvarchar(1000)", "datetime2"],
            values: ["https://wms.geonorge.no/skwms1/wms.adm_enheter2?", "kommuner_gjel,fylker_gjel", "Geonorge", new DateTime(2026, 9, 21)]);
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UpdateMdirWmsAdresses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                                UPDATE dbo.MapLayer
                                SET Url = N'https://kart3.miljodirektoratet.no/arcgis/services/vern/MapServer/WMSServer?',
                                    Layers = N'naturvern_klasser_omraade,foreslaatt_naturvern_omraade'
                                WHERE Id = 3
                                AND Name = 'Vern';

                                IF @@ROWCOUNT <> 1
                                THROW 50000, 'Expected exactly one MapLayer row for Vern.', 1;
                             """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                                UPDATE dbo.MapLayer
                                SET Url = N'https://wms.miljodirektoratet.no/arcgis/services/vern/mapserver/WMSServer?',
                                    Layers = N'naturvern_klasser_omrade,foreslatt_naturvern_omrade'
                                WHERE Id = 3
                                AND Name = 'Vern';

                                IF @@ROWCOUNT <> 1
                                THROW 50000, 'Expected exactly one MapLayer row for Vern.', 1;
                             """);
    }
}

using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Artskart3.Infrastructure.Migrations;

[DbContext(typeof(ArtskartDbContext))]
[Migration("20261006020000_CombineMatrikkelMapLayers")]
public partial class CombineMatrikkelMapLayers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [MapLayer]
            SET [Name] = N'Eiendomskart', [Layers] = N'eiendomsgrense,eiendoms_id',
                [UpdatedAt] = '2026-10-06T00:00:00'
            WHERE [Name] = N'Eiendomsgrense'
                AND NOT EXISTS (SELECT 1 FROM [MapLayer] WHERE [Name] = N'Eiendomskart');

            DELETE FROM [MapLayer]
            WHERE [Name] IN (N'Eiendomsgrense', N'EiendomsID (Gnr/Bnr/Fnr)');

            IF NOT EXISTS (SELECT 1 FROM [MapLayer] WHERE [Name] = N'Eiendomskart')
            BEGIN
                INSERT INTO [MapLayer]
                    ([Name], [Type], [Url], [Layers], [Format], [Version], [Attribution],
                     [CreatedAt], [UpdatedAt], [IsDeleted])
                VALUES
                    (N'Eiendomskart', N'WMS', N'https://wms.geonorge.no/skwms1/wms.matrikkel',
                     N'eiendomsgrense,eiendoms_id', N'image/png', N'1.3.0', N'Kartverket',
                     '2026-10-06T00:00:00', '2026-10-06T00:00:00', 0);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [MapLayer] WHERE [Name] = N'Eiendomskart';
            """);
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RemoveLegacyGrenserMapLayer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "SortOrder",
            table: "MapLayer",
            type: "int",
            nullable: false,
            defaultValue: 50,
            oldClrType: typeof(int),
            oldType: "int",
            oldDefaultValue: 1000);
        migrationBuilder.Sql("""
                DELETE FROM [MapLayer]
                WHERE [Name] = N'Grenser'
                    AND [Url] = N'https://wms.geonorge.no/skwms1/wms.adm_enheter2?'
                    AND [Layers] = N'kommuner_gjel';
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "SortOrder",
            table: "MapLayer",
            type: "int",
            nullable: false,
            defaultValue: 1000,
            oldClrType: typeof(int),
            oldType: "int",
            oldDefaultValue: 50);
        migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [MapLayer] WHERE [Name] = N'Grenser')
                BEGIN
                    INSERT INTO [MapLayer]
                        ([Name], [Type], [Url], [Layers], [Format], [Version], [Attribution],
                         [CreatedAt], [UpdatedAt], [IsDeleted], [SortOrder])
                    VALUES
                        (N'Grenser', N'WMS', N'https://wms.geonorge.no/skwms1/wms.adm_enheter2?',
                         N'kommuner_gjel', N'image/png', N'1.3.0', N'admGrenserAttribution',
                         '2026-09-14T00:00:00', '2026-09-14T00:00:00', 0, 1000);
                END;
                """);
    }
}

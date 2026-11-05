using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UpdateMapLayerTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "MaxScaleDenominator",
            table: "MapLayer",
            type: "float",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "MinScaleDenominator",
            table: "MapLayer",
            type: "float",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Opacity",
            table: "MapLayer",
            type: "float",
            nullable: false,
            defaultValue: 1.0);

        migrationBuilder.AddColumn<int>(
            name: "RetryAfterSeconds",
            table: "MapLayer",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "UseSingleImage",
            table: "MapLayer",
            type: "bit",
            nullable: false,
            defaultValue: false);

        // IX_MapLayer_Name er ikke unik, så raden settes bare inn hvis den mangler.
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM [MapLayer] WHERE [Name] = N'MaritimeGrenser')
            BEGIN
                INSERT INTO [MapLayer]
                    ([Name], [Type], [Url], [Layers], [Format], [Version], [Attribution],
                     [CreatedAt], [UpdatedAt], [IsDeleted], [SortOrder])
                VALUES
                    (N'MaritimeGrenser', N'WMS', N'https://wms.geonorge.no/skwms1/wms.nmg?',
                     N'Maritime_grenser,Maritime_grenser_navn',
                     N'image/png', N'1.3.0', N'Kartverket',
                     GETUTCDATE(), GETUTCDATE(), 0, 60);
            END;

            UPDATE [MapLayer]
            SET [Opacity] = 0.5
            WHERE [Name] IN (N'Bioseksjoner', N'Biosoner');

            UPDATE [MapLayer]
            SET [UseSingleImage] = 1,
                [MaxScaleDenominator] = 100000,
                [RetryAfterSeconds] = 30
            WHERE [Name] = N'Eiendomskart';

            UPDATE [MapLayer]
            SET [UseSingleImage] = 1
            WHERE [Name] = N'MaritimeGrenser';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [MapLayer]
            WHERE [Name] = N'MaritimeGrenser'
                AND [Url] = N'https://wms.geonorge.no/skwms1/wms.nmg?'
                AND [Layers] = N'Maritime_grenser,Maritime_grenser_navn';
            """);

        migrationBuilder.DropColumn(
            name: "MaxScaleDenominator",
            table: "MapLayer");

        migrationBuilder.DropColumn(
            name: "MinScaleDenominator",
            table: "MapLayer");

        migrationBuilder.DropColumn(
            name: "Opacity",
            table: "MapLayer");

        migrationBuilder.DropColumn(
            name: "RetryAfterSeconds",
            table: "MapLayer");

        migrationBuilder.DropColumn(
            name: "UseSingleImage",
            table: "MapLayer");
    }
}

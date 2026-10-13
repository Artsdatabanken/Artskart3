using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMapLayerRenderingParameters : Migration
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

        migrationBuilder.Sql("""
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

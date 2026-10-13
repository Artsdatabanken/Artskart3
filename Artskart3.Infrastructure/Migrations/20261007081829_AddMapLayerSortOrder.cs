using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMapLayerSortOrder : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "MapLayer",
            type: "int",
            nullable: false,
            defaultValue: 1000);

        migrationBuilder.Sql("""
                UPDATE [MapLayer]
                SET [SortOrder] = CASE [Name]
                    WHEN N'Eiendomskart' THEN 10
                    WHEN N'admGrenser' THEN 20
                    WHEN N'Vern' THEN 30
                    WHEN N'Bioseksjoner' THEN 40
                    WHEN N'Biosoner' THEN 50
                    ELSE 1000
                END;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SortOrder",
            table: "MapLayer");
    }
}

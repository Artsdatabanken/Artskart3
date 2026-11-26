using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAlienSpeciesSearchIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AlienSpeciesCategoryId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OEI_AreaAlienSpeciesCategory",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "AlienSpeciesCategoryId" },
            filter: "[AlienSpeciesCategoryId] IS NOT NULL")
            .Annotation("SqlServer:Include", new[] { "DateTimeCollected", "ObservationId" })
            .Annotation("SqlServer:Online", true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OEI_AreaAlienSpeciesCategory",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "AlienSpeciesCategoryId",
            table: "ObservationEntityIndex");
    }
}

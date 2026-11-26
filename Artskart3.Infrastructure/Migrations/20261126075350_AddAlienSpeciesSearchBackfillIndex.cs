using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAlienSpeciesSearchBackfillIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_OEI_AlienSpeciesUnmaterialized",
            table: "ObservationEntityIndex",
            columns: new[] { "ObservationId", "EntityTypeId", "EntityId" },
            filter: "[AlienSpeciesCategoryId] IS NULL")
            .Annotation("SqlServer:Online", true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OEI_AlienSpeciesUnmaterialized",
            table: "ObservationEntityIndex");
    }
}

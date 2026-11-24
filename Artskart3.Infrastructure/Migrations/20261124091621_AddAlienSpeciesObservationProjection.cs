using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAlienSpeciesObservationProjection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AlienSpeciesCategoryId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AlienSpeciesCategoryId",
            table: "Observation",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OEI_AreaAlienSpeciesCategory",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "AlienSpeciesCategoryId" },
            filter: "[AlienSpeciesCategoryId] IS NOT NULL")
            .Annotation("SqlServer:Include", new[] { "DateTimeCollected", "ObservationId" })
            .Annotation("SqlServer:Online", true);

        migrationBuilder.CreateIndex(
            name: "IX_Observation_AlienSpeciesCategoryId",
            table: "Observation",
            column: "AlienSpeciesCategoryId",
            filter: "[AlienSpeciesCategoryId] IS NOT NULL")
            .Annotation("SqlServer:Online", true);

        migrationBuilder.AddForeignKey(
            name: "FK_dbo.Observation_dbo.Category_AlienSpeciesCategoryId",
            table: "Observation",
            column: "AlienSpeciesCategoryId",
            principalTable: "Category",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_dbo.Observation_dbo.Category_AlienSpeciesCategoryId",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_OEI_AreaAlienSpeciesCategory",
            table: "ObservationEntityIndex");

        migrationBuilder.DropIndex(
            name: "IX_Observation_AlienSpeciesCategoryId",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "AlienSpeciesCategoryId",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "AlienSpeciesCategoryId",
            table: "Observation");
    }
}

using Artskart3.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddObservationEntityIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ObservationEntityIndex",
            columns: table => new
            {
                ObservationId = table.Column<int>(type: "int", nullable: false),
                EntityTypeId = table.Column<int>(type: "int", nullable: false),
                EntityId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ObservationEntityIndex", x => new { x.ObservationId, x.EntityTypeId, x.EntityId });
            });

        var restrictedArea = (int)ObservationIndexEntityType.RestrictedArea;
        var institution = (int)ObservationIndexEntityType.Institution;

        // Populer områder fra Observation -> Location -> LocationAreas -> Area
        // AreaTypeId i Area-tabellen speiler ObservationIndexEntityType 1-4 direkte.
        // Fid konverteres til int:
        //   - RestrictedArea: fjern "Naturbase VV"-prefiks
        //   - Andre: fjern eventuell "_" (gjelder historiske fylkes-Fid-er som "15_2017")
        migrationBuilder.Sql($@"
INSERT INTO dbo.ObservationEntityIndex (ObservationId, EntityTypeId, EntityId)
SELECT DISTINCT o.Id, a.AreaTypeId,
    CASE
        WHEN a.AreaTypeId = {restrictedArea} THEN CAST(REPLACE(a.Fid, 'Naturbase VV', '') AS INT)
        ELSE CAST(REPLACE(a.Fid, '_', '') AS INT)
    END
FROM dbo.Observation o
JOIN dbo.Location l ON l.Id = o.LocationId
JOIN dbo.LocationAreas la ON la.LocationId = l.Id
JOIN dbo.Area a ON a.Id = la.AreaId
WHERE a.IsCurrent = 1
  AND o.LocationId IS NOT NULL;
");

        // Populer institusjoner (OrganizationType.Institution = 1)
        migrationBuilder.Sql($@"
INSERT INTO dbo.ObservationEntityIndex (ObservationId, EntityTypeId, EntityId)
SELECT DISTINCT r.ObservationId, {institution}, r.OrganizationId
FROM dbo.OrganizationRelation r
JOIN dbo.Organization org ON org.Id = r.OrganizationId
WHERE org.OrganizationTypeId = {(int)OrganizationType.Institution};
");

        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_Lookup",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_ObservationId",
            table: "ObservationEntityIndex",
            column: "ObservationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ObservationEntityIndex");
    }
}

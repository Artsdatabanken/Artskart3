using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationAreaIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObservationAreaIndex",
                columns: table => new
                {
                    ObservationId = table.Column<int>(type: "int", nullable: false),
                    AreaTypeId = table.Column<int>(type: "int", nullable: false),
                    AreaFid = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationAreaIndex", x => new { x.ObservationId, x.AreaTypeId, x.AreaFid });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObservationAreaIndex_Lookup",
                table: "ObservationAreaIndex",
                columns: new[] { "AreaTypeId", "AreaFid" })
                .Annotation("SqlServer:Include", new[] { "ObservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ObservationAreaIndex_ObservationId",
                table: "ObservationAreaIndex",
                column: "ObservationId")
                .Annotation("SqlServer:Include", new[] { "AreaTypeId", "AreaFid" });

            // Populer fra Observation -> Location -> LocationAreas -> Area
            migrationBuilder.Sql(@"
INSERT INTO dbo.ObservationAreaIndex (ObservationId, AreaTypeId, AreaFid)
SELECT DISTINCT o.Id, a.AreaTypeId, a.Fid
FROM dbo.Observation o
JOIN dbo.Location l ON l.Id = o.LocationId
JOIN dbo.LocationAreas la ON la.LocationId = l.Id
JOIN dbo.Area a ON a.Id = la.AreaId
WHERE a.IsCurrent = 1
  AND o.LocationId IS NOT NULL;
");

            // Populer institusjoner (OrganizationTypeId = 1) som AreaTypeId = 5
            migrationBuilder.Sql(@"
INSERT INTO dbo.ObservationAreaIndex (ObservationId, AreaTypeId, AreaFid)
SELECT DISTINCT r.ObservationId, 5, CAST(r.OrganizationId AS NVARCHAR(450))
FROM dbo.OrganizationRelation r
JOIN dbo.Organization org ON org.Id = r.OrganizationId
WHERE org.OrganizationTypeId = 1;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ObservationAreaIndex_ObservationId",
                table: "ObservationAreaIndex");

            migrationBuilder.DropIndex(
                name: "IX_ObservationAreaIndex_Lookup",
                table: "ObservationAreaIndex");

            migrationBuilder.DropTable(
                name: "ObservationAreaIndex");
        }
    }
}

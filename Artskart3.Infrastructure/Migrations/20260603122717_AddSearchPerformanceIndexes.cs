using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelation_OrgId_ObsId",
                table: "OrganizationRelation",
                columns: new[] { "OrganizationId", "ObservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaId_LocationId",
                table: "LocationAreas",
                columns: new[] { "AreaId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Area_AreaTypeId_Fid_IsCurrent",
                table: "Area",
                columns: new[] { "AreaTypeId", "Fid", "IsCurrent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationRelation_OrgId_ObsId",
                table: "OrganizationRelation");

            migrationBuilder.DropIndex(
                name: "IX_AreaId_LocationId",
                table: "LocationAreas");

            migrationBuilder.DropIndex(
                name: "IX_Area_AreaTypeId_Fid_IsCurrent",
                table: "Area");
        }
    }
}

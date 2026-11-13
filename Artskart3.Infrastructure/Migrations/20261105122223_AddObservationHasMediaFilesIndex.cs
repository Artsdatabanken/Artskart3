using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddObservationHasMediaFilesIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Observation_HasMediaFiles",
            table: "Observation",
            columns: new[] { "HasMediaFiles", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[HasMediaFiles] = 1")
            .Annotation("SqlServer:Include", new[] { "BasisOfRecordId", "BehaviorId", "CategoryId", "CoordinatePrecisionInMeters", "DatasetOrgId", "InstitutionOrgId", "MonthCollected", "RegistrationStatusId", "TaxonGroupId", "TaxonId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_HasMediaFiles",
            table: "Observation");
    }
}

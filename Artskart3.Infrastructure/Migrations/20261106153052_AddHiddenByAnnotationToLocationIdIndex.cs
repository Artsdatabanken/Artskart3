using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddHiddenByAnnotationToLocationIdIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_LocationId",
            table: "Observation");

        migrationBuilder.CreateIndex(
            name: "IX_LocationId",
            table: "Observation",
            columns: new[] { "LocationId", "CategoryId" })
            .Annotation("SqlServer:DataCompression", DataCompressionType.Page)
            .Annotation("SqlServer:Include", new[] { "DateTimeCollected", "BasisOfRecordId", "TaxonId", "TaxonGroupId", "YearCollected", "MonthCollected", "Id", "HiddenByAnnotation" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_LocationId",
            table: "Observation");

        migrationBuilder.CreateIndex(
            name: "IX_LocationId",
            table: "Observation",
            columns: new[] { "LocationId", "CategoryId" })
            .Annotation("SqlServer:DataCompression", DataCompressionType.Page)
            .Annotation("SqlServer:Include", new[] { "DateTimeCollected", "BasisOfRecordId", "TaxonId", "TaxonGroupId", "YearCollected", "MonthCollected", "Id" });
    }
}

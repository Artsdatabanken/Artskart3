using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddListViewIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_OEI_AreaListView",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "DateTimeCollected", "ObservationId" },
            descending: new[] { false, false, true, true })
            .Annotation("SqlServer:DataCompression", DataCompressionType.Page)
            .Annotation("SqlServer:Include", new[] { "TaxonGroupId", "CategoryId", "HiddenByAnnotation" })
            .Annotation("SqlServer:Online", true);

        migrationBuilder.CreateIndex(
            name: "IX_OEI_MediaListView",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId", "DateTimeCollected", "ObservationId" },
            descending: new[] { false, false, true, true },
            filter: "[HasMediaFiles] = 1")
            .Annotation("SqlServer:DataCompression", DataCompressionType.Page)
            .Annotation("SqlServer:Include", new[] { "CategoryId", "TaxonGroupId", "HiddenByAnnotation" })
            .Annotation("SqlServer:Online", true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OEI_AreaListView",
            table: "ObservationEntityIndex");

        migrationBuilder.DropIndex(
            name: "IX_OEI_MediaListView",
            table: "ObservationEntityIndex");
    }
}

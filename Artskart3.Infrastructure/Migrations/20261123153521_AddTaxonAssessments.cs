using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTaxonAssessments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TaxonAssessment",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ExternalTaxonId = table.Column<int>(type: "int", nullable: false),
                TaxonId = table.Column<int>(type: "int", nullable: false),
                CategoryTypeId = table.Column<int>(type: "int", nullable: false),
                CategoryId = table.Column<int>(type: "int", nullable: false),
                Area = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                ExternalAssessmentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Source = table.Column<int>(type: "int", nullable: false),
                AssessmentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsEffective = table.Column<bool>(type: "bit", nullable: false),
                IsRetained = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dbo.TaxonAssessment", x => x.Id);
                table.ForeignKey(
                    name: "FK_dbo.TaxonAssessment_dbo.CategoryType_CategoryTypeId",
                    column: x => x.CategoryTypeId,
                    principalTable: "CategoryType",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_dbo.TaxonAssessment_dbo.Category_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "Category",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_dbo.TaxonAssessment_dbo.Taxon_TaxonId",
                    column: x => x.TaxonId,
                    principalTable: "Taxon",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TaxonAssessmentSyncRun",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CategoryTypeId = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CurrentPageCount = table.Column<int>(type: "int", nullable: false),
                CurrentAssessmentCount = table.Column<int>(type: "int", nullable: false),
                SnapshotAssessmentCount = table.Column<int>(type: "int", nullable: false),
                AppliedAssessmentCount = table.Column<int>(type: "int", nullable: false),
                RetainedAssessmentCount = table.Column<int>(type: "int", nullable: false),
                SkippedAssessmentCount = table.Column<int>(type: "int", nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dbo.TaxonAssessmentSyncRun", x => x.Id);
                table.ForeignKey(
                    name: "FK_dbo.TaxonAssessmentSyncRun_dbo.CategoryType_CategoryTypeId",
                    column: x => x.CategoryTypeId,
                    principalTable: "CategoryType",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Taxon_ExternalTaxonId",
            table: "Taxon",
            column: "ExternalTaxonId");

        migrationBuilder.CreateIndex(
            name: "UX_Category_CategoryTypeId_Code",
            table: "Category",
            columns: new[] { "CategoryTypeId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_TaxonAssessment_CategoryId",
            table: "TaxonAssessment",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_TaxonAssessment_ExternalLookup",
            table: "TaxonAssessment",
            columns: new[] { "CategoryTypeId", "ExternalTaxonId", "Area" });

        migrationBuilder.CreateIndex(
            name: "UX_TaxonAssessment_Effective",
            table: "TaxonAssessment",
            columns: new[] { "TaxonId", "CategoryTypeId", "Area" },
            unique: true,
            filter: "[IsEffective] = 1 AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "UX_TaxonAssessment_Source",
            table: "TaxonAssessment",
            columns: new[] { "TaxonId", "CategoryTypeId", "Area", "Source" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_TaxonAssessmentSyncRun_Status",
            table: "TaxonAssessmentSyncRun",
            columns: new[] { "CategoryTypeId", "Status", "StartedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TaxonAssessment");

        migrationBuilder.DropTable(
            name: "TaxonAssessmentSyncRun");

        migrationBuilder.DropIndex(
            name: "IX_Taxon_ExternalTaxonId",
            table: "Taxon");

        migrationBuilder.DropIndex(
            name: "UX_Category_CategoryTypeId_Code",
            table: "Category");
    }
}

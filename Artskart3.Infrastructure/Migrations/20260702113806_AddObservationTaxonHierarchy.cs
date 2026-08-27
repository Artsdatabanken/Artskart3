using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddObservationTaxonHierarchy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "ObservationTaxonHierarchy",
                columns: table => new
                {
                    ObservationId = table.Column<int>(type: "int", nullable: false),
                    KingdomTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubkingdomTaxonId = table.Column<int>(type: "int", nullable: true),
                    PhylumTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubphylumTaxonId = table.Column<int>(type: "int", nullable: true),
                    SuperclassTaxonId = table.Column<int>(type: "int", nullable: true),
                    ClassTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubclassTaxonId = table.Column<int>(type: "int", nullable: true),
                    InfraclassTaxonId = table.Column<int>(type: "int", nullable: true),
                    CohortTaxonId = table.Column<int>(type: "int", nullable: true),
                    SuperorderTaxonId = table.Column<int>(type: "int", nullable: true),
                    OrderTaxonId = table.Column<int>(type: "int", nullable: true),
                    SuborderTaxonId = table.Column<int>(type: "int", nullable: true),
                    InfraorderTaxonId = table.Column<int>(type: "int", nullable: true),
                    SuperfamilyTaxonId = table.Column<int>(type: "int", nullable: true),
                    FamilyTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubfamilyTaxonId = table.Column<int>(type: "int", nullable: true),
                    TribeTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubtribeTaxonId = table.Column<int>(type: "int", nullable: true),
                    GenusTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubgenusTaxonId = table.Column<int>(type: "int", nullable: true),
                    SectionTaxonId = table.Column<int>(type: "int", nullable: true),
                    SpeciesTaxonId = table.Column<int>(type: "int", nullable: true),
                    SubspeciesTaxonId = table.Column<int>(type: "int", nullable: true),
                    VarietyTaxonId = table.Column<int>(type: "int", nullable: true),
                    FormTaxonId = table.Column<int>(type: "int", nullable: true),
                    NotSetTaxonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationTaxonHierarchy", x => x.ObservationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Class",
                table: "ObservationTaxonHierarchy",
                column: "ClassTaxonId",
                filter: "[ClassTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Cohort",
                table: "ObservationTaxonHierarchy",
                column: "CohortTaxonId",
                filter: "[CohortTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Family",
                table: "ObservationTaxonHierarchy",
                column: "FamilyTaxonId",
                filter: "[FamilyTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Form",
                table: "ObservationTaxonHierarchy",
                column: "FormTaxonId",
                filter: "[FormTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Genus",
                table: "ObservationTaxonHierarchy",
                column: "GenusTaxonId",
                filter: "[GenusTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Infraclass",
                table: "ObservationTaxonHierarchy",
                column: "InfraclassTaxonId",
                filter: "[InfraclassTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Infraorder",
                table: "ObservationTaxonHierarchy",
                column: "InfraorderTaxonId",
                filter: "[InfraorderTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Kingdom",
                table: "ObservationTaxonHierarchy",
                column: "KingdomTaxonId",
                filter: "[KingdomTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_NotSet",
                table: "ObservationTaxonHierarchy",
                column: "NotSetTaxonId",
                filter: "[NotSetTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Order",
                table: "ObservationTaxonHierarchy",
                column: "OrderTaxonId",
                filter: "[OrderTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Phylum",
                table: "ObservationTaxonHierarchy",
                column: "PhylumTaxonId",
                filter: "[PhylumTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Section",
                table: "ObservationTaxonHierarchy",
                column: "SectionTaxonId",
                filter: "[SectionTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Species",
                table: "ObservationTaxonHierarchy",
                column: "SpeciesTaxonId",
                filter: "[SpeciesTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subclass",
                table: "ObservationTaxonHierarchy",
                column: "SubclassTaxonId",
                filter: "[SubclassTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subfamily",
                table: "ObservationTaxonHierarchy",
                column: "SubfamilyTaxonId",
                filter: "[SubfamilyTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subgenus",
                table: "ObservationTaxonHierarchy",
                column: "SubgenusTaxonId",
                filter: "[SubgenusTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subkingdom",
                table: "ObservationTaxonHierarchy",
                column: "SubkingdomTaxonId",
                filter: "[SubkingdomTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Suborder",
                table: "ObservationTaxonHierarchy",
                column: "SuborderTaxonId",
                filter: "[SuborderTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subphylum",
                table: "ObservationTaxonHierarchy",
                column: "SubphylumTaxonId",
                filter: "[SubphylumTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subspecies",
                table: "ObservationTaxonHierarchy",
                column: "SubspeciesTaxonId",
                filter: "[SubspeciesTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Subtribe",
                table: "ObservationTaxonHierarchy",
                column: "SubtribeTaxonId",
                filter: "[SubtribeTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Superclass",
                table: "ObservationTaxonHierarchy",
                column: "SuperclassTaxonId",
                filter: "[SuperclassTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Superfamily",
                table: "ObservationTaxonHierarchy",
                column: "SuperfamilyTaxonId",
                filter: "[SuperfamilyTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Superorder",
                table: "ObservationTaxonHierarchy",
                column: "SuperorderTaxonId",
                filter: "[SuperorderTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Tribe",
                table: "ObservationTaxonHierarchy",
                column: "TribeTaxonId",
                filter: "[TribeTaxonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OTH_Variety",
                table: "ObservationTaxonHierarchy",
                column: "VarietyTaxonId",
                filter: "[VarietyTaxonId] IS NOT NULL");

            // Slå på sidekomprimering FØR tabellen fylles.
            //
            // EF Core støtter ikke DATA_COMPRESSION, så CreateTable/CreateIndex over
            // gir ukomprimerte strukturer. Målt på ferdig fylt tabell utgjorde det
            // ~21 GB (klyngeindeks + 25 rangindekser) mot ~10 GB komprimert.
            //
            // Her er tabellen fortsatt tom, så denne rebuilden er momentan — og
            // INSERT-en under skriver da rett inn i komprimerte sider. Gjøres dette
            // etterpå i stedet, koster det 15-30 minutter.
            migrationBuilder.Sql(@"
ALTER INDEX ALL ON dbo.ObservationTaxonHierarchy
    REBUILD WITH (DATA_COMPRESSION = PAGE);
");

            // Populer hierarkitabellen fra Taxon.TaxonIdHiarchy
            migrationBuilder.Sql(@"
INSERT INTO dbo.ObservationTaxonHierarchy (
    ObservationId, KingdomTaxonId, SubkingdomTaxonId, PhylumTaxonId, SubphylumTaxonId,
    SuperclassTaxonId, ClassTaxonId, SubclassTaxonId, InfraclassTaxonId, CohortTaxonId,
    SuperorderTaxonId, OrderTaxonId, SuborderTaxonId, InfraorderTaxonId, SuperfamilyTaxonId,
    FamilyTaxonId, SubfamilyTaxonId, TribeTaxonId, SubtribeTaxonId, GenusTaxonId,
    SubgenusTaxonId, SectionTaxonId, SpeciesTaxonId, SubspeciesTaxonId, VarietyTaxonId,
    FormTaxonId, NotSetTaxonId)
SELECT
    o.Id,
    MAX(CASE WHEN ancestor.TaxonRankId = 1 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 2 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 3 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 4 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 5 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 6 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 7 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 8 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 9 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 10 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 11 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 12 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 13 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 14 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 15 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 16 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 17 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 18 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 19 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 20 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 21 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 22 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 23 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 24 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 25 THEN ancestor.Id END),
    MAX(CASE WHEN ancestor.TaxonRankId = 26 THEN ancestor.Id END)
FROM dbo.Observation o
JOIN dbo.Taxon t ON o.TaxonId = t.Id
CROSS APPLY STRING_SPLIT(t.TaxonIdHiarchy, ',') AS s
JOIN dbo.Taxon ancestor ON ancestor.Id = TRY_CAST(s.value AS INT)
WHERE o.IsDeleted = 0
GROUP BY o.Id;
");
        }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ObservationTaxonHierarchy");
    }
}

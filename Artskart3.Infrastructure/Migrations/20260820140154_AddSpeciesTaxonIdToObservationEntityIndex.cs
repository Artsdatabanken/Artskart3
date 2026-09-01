using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSpeciesTaxonIdToObservationEntityIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SpeciesTaxonId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "GenusTaxonId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FamilyTaxonId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OrderTaxonId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        // Kolonnene fylles manuelt via Scripts/BackfillTaxonHierarchyColumns.sql
        // på grunn av lang kjøretid (190M+ rader).
        //
        // MERK: Det opprettes bevisst INGEN rowstore-indekser på disse kolonnene.
        // Vi prøvde filtrerte indekser på (RangTaxonId, EntityTypeId, EntityId) og målte
        // at de gjorde spørringene TREGERE — optimalisereren valgte dem av og til, og
        // tok feil hver gang. Familie gikk 1369 -> 248 ms og slekt 838 -> 227 ms da de
        // ble fjernet. Kostnadsmodellen undervurderer batch-mode-gjennomstrømming i
        // columnstore mot et b-tre-seek. Kolonnene filtreres via columnstore i stedet.

        // Bygger en b-tre-indeks over 60M rader — tar noen minutter.
        // Rå SQL fordi EF Core ikke støtter DATA_COMPRESSION; alle andre indekser
        // på Observation er sidekomprimert, og uten dette blir denne ~2x så stor.
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonId
ON dbo.Observation (TaxonId)
WITH (DATA_COMPRESSION = PAGE);
");

        // Columnstore-indeksen opprettes i egen migrasjon
        // (20260820150000_AddObservationEntityIndexColumnstore), fordi den tar
        // 10-30 minutter og må kjøre utenfor migrasjonstransaksjonen.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IX_Observation_TaxonId ON dbo.Observation;");

        migrationBuilder.DropColumn(name: "OrderTaxonId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "FamilyTaxonId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "GenusTaxonId", table: "ObservationEntityIndex");
        migrationBuilder.DropColumn(name: "SpeciesTaxonId", table: "ObservationEntityIndex");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// CompleteFilter, steg 1 — skjemaet for å kunne besvare alle frontend-filtre
/// uten subquery mot Observation-tabellen.
///
/// Alt her er metadataoperasjoner eller tomme objekter, så migrasjonen går på
/// sekunder selv om Observation har 61M rader og ObservationEntityIndex 192M:
/// nullable kolonner uten default legges til i metadata, og ObservationDataset
/// opprettes tom.
///
/// HVA SOM KOMMER HVOR:
/// - Institusjon (54 distinkte verdier), samling (1 908) og atferd (6) er
///   lavselektive og verifisert 1:1 per observasjon. De blir kolonner, og skal
///   betjenes av columnstore-indeksen — ikke av rowstore-indekser. Kolonnene
///   legges til i IX_OEI_Columnstore i en senere migrasjon, etter backfillen.
/// - Datasett er IKKE 1:1 (745 066 observasjoner har flere enn ett, maks 5), så
///   det blir en egen smal tabell i stedet for en kolonne. En kolonne ville
///   stille droppet tilknytningen for de ~750 000.
/// - Katalognummer får ingen denormalisering i det hele tatt. Det er tilnærmet
///   unikt (54 687 713 distinkte verdier over 61 052 216 observasjoner), og
///   filteret sender ObservationId-er fra typeahead-endepunktet. Indeksen som
///   betjener typeaheaden opprettes i egen migrasjon fordi den bygges over 61M
///   rader og ikke bør kjøre inne i migrasjonstransaksjonen.
///
/// FREMMEDNØKLER MED RÅ SQL:
/// InstitutionOrgId og CollectionOrgId er ikke modellert som relasjoner i
/// DbContext. EF Core lager automatisk en indeks bak hver fremmednøkkel, og på
/// 61M rader ville det gitt to rowstore-indekser vi har målt grunn til å tro er
/// skadelige. Constraintene opprettes derfor direkte i SQL — databasen får
/// referanseintegriteten uten indeksene. Kolonnene er tomme (alle NULL) når
/// constraintene legges til, så valideringen er triviell.
///
/// Kolonnene er nullable her. De settes til NOT NULL i oppryddingssteget, etter
/// at backfillen har fylt dem og verifiseringen er kjørt i alle miljøer.
/// </summary>
public partial class AddCompleteFilterColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "BehaviorId",
            table: "ObservationEntityIndex",
            type: "tinyint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CollectionOrgId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "InstitutionOrgId",
            table: "ObservationEntityIndex",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CollectionOrgId",
            table: "Observation",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "InstitutionOrgId",
            table: "Observation",
            type: "int",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ObservationDataset",
            columns: table => new
            {
                ObservationId = table.Column<int>(type: "int", nullable: false),
                DatasetOrgId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ObservationDataset", x => new { x.ObservationId, x.DatasetOrgId });
                table.ForeignKey(
                    name: "FK_ObservationDataset_Observation",
                    column: x => x.ObservationId,
                    principalTable: "Observation",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ObservationDataset_Organization",
                    column: x => x.DatasetOrgId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ObservationDataset_Dataset",
            table: "ObservationDataset",
            columns: new[] { "DatasetOrgId", "ObservationId" });

        // Sidekomprimering. EF Core har ingen parameter for DATA_COMPRESSION, så
        // det må settes med rå SQL — samme grunn som i migrasjon 20260820140154.
        // Tabellen er tom her, så begge REBUILD-ene er umiddelbare. Det er med
        // vilje at det gjøres nå og ikke etter backfillen: da ville det vært en
        // rebuild av 14,5M rader.
        migrationBuilder.Sql(@"
ALTER TABLE dbo.ObservationDataset REBUILD WITH (DATA_COMPRESSION = PAGE);
ALTER INDEX IX_ObservationDataset_Dataset ON dbo.ObservationDataset
    REBUILD WITH (DATA_COMPRESSION = PAGE);
");

        // Fremmednøkler uten indeks — se klassekommentaren.
        migrationBuilder.Sql(@"
ALTER TABLE dbo.Observation WITH CHECK
    ADD CONSTRAINT FK_Observation_Organization_InstitutionOrgId
    FOREIGN KEY (InstitutionOrgId) REFERENCES dbo.Organization (Id);

ALTER TABLE dbo.Observation WITH CHECK
    ADD CONSTRAINT FK_Observation_Organization_CollectionOrgId
    FOREIGN KEY (CollectionOrgId) REFERENCES dbo.Organization (Id);
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE dbo.Observation DROP CONSTRAINT IF EXISTS FK_Observation_Organization_InstitutionOrgId;
ALTER TABLE dbo.Observation DROP CONSTRAINT IF EXISTS FK_Observation_Organization_CollectionOrgId;
");

        migrationBuilder.DropTable(
            name: "ObservationDataset");

        migrationBuilder.DropColumn(
            name: "BehaviorId",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "CollectionOrgId",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "InstitutionOrgId",
            table: "ObservationEntityIndex");

        migrationBuilder.DropColumn(
            name: "CollectionOrgId",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "InstitutionOrgId",
            table: "Observation");
    }
}

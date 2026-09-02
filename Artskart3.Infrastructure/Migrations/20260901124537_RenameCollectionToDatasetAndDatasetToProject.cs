using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Retter navnene i skjemaet etter at organisasjonstypene fikk GBIF-navn:
/// type 2 heter Dataset, type 3 heter Project.
///
///   Observation.CollectionOrgId            -> DatasetOrgId        (type 2)
///   ObservationEntityIndex.CollectionOrgId -> DatasetOrgId        (type 2)
///   ObservationDataset                     -> ObservationProject  (type 3)
///   ObservationDataset.DatasetOrgId        -> ProjectOrgId        (type 3)
///
/// NAVNENE BYTTER PLASS, DE FLYTTER SEG IKKE ETT HAKK. «DatasetOrgId» betyr type 3
/// før denne migrasjonen og type 2 etter. Leser du en spørring eller en plan fra
/// før 1. september 2026, betyr samme kolonnenavn noe annet.
///
/// HÅNDSKREVET MED VILJE — IKKE REGENERER DENNE MED «dotnet ef migrations add».
/// EF scaffoldet DropTable + CreateTable for tabellomdøpingen, ikke RenameTable.
/// Det ville slettet alle 14 520 793 radene i ObservationDataset og gjenskapt
/// tabellen tom, uten annen advarsel enn «An operation was scaffolded that may
/// result in the loss of data». Kolonnene fikk riktig RenameColumn; det var kun
/// tabellen differ-en ikke kjente igjen som en omdøping.
///
/// Alt her er metadataoperasjoner (sp_rename) og går på millisekunder selv på 61M
/// og 192M rader — ingen data flyttes. Derfor også ingen suppressTransaction:
/// migrasjonen skal være alt-eller-ingenting, slik at et avbrudd ikke etterlater
/// halvt omdøpte navn.
///
/// Tidligere migrasjoner og Scripts/BackfillAll.sql beholder de gamle navnene.
/// De beskriver skjemaet slik det var da de kjørte, og på et ferskt miljø kjører
/// de før denne.
/// </summary>
public partial class RenameCollectionToDatasetAndDatasetToProject : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---- type 2: Collection -> Dataset ----
        migrationBuilder.RenameColumn(
            name: "CollectionOrgId",
            table: "Observation",
            newName: "DatasetOrgId");

        migrationBuilder.RenameColumn(
            name: "CollectionOrgId",
            table: "ObservationEntityIndex",
            newName: "DatasetOrgId");

        // ---- type 3: Dataset -> Project ----
        // Tabellen først, deretter kolonnen: sp_rename for en kolonne adresseres
        // som 'tabell.kolonne', så kolonnen må vises til med det NYE tabellnavnet.
        migrationBuilder.RenameTable(
            name: "ObservationDataset",
            newName: "ObservationProject");

        migrationBuilder.RenameColumn(
            name: "DatasetOrgId",
            table: "ObservationProject",
            newName: "ProjectOrgId");

        migrationBuilder.RenameIndex(
            name: "IX_ObservationDataset_Dataset",
            table: "ObservationProject",
            newName: "IX_ObservationProject_Project");

        // sp_rename på en tabell døper ikke om nøkler og fremmednøkler. Uten dette
        // ville databasen hatt PK_ObservationDataset på tabellen ObservationProject,
        // og modellen ville ment noe annet enn skjemaet ved neste migrasjon.
        migrationBuilder.Sql(@"
EXEC sp_rename N'dbo.PK_ObservationDataset',            N'PK_ObservationProject',            N'OBJECT';
EXEC sp_rename N'dbo.FK_ObservationDataset_Observation',   N'FK_ObservationProject_Observation',   N'OBJECT';
EXEC sp_rename N'dbo.FK_ObservationDataset_Organization',  N'FK_ObservationProject_Organization',  N'OBJECT';
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
EXEC sp_rename N'dbo.FK_ObservationProject_Organization',  N'FK_ObservationDataset_Organization',  N'OBJECT';
EXEC sp_rename N'dbo.FK_ObservationProject_Observation',   N'FK_ObservationDataset_Observation',   N'OBJECT';
EXEC sp_rename N'dbo.PK_ObservationProject',            N'PK_ObservationDataset',            N'OBJECT';
");

        migrationBuilder.RenameIndex(
            name: "IX_ObservationProject_Project",
            table: "ObservationProject",
            newName: "IX_ObservationDataset_Dataset");

        migrationBuilder.RenameColumn(
            name: "ProjectOrgId",
            table: "ObservationProject",
            newName: "DatasetOrgId");

        migrationBuilder.RenameTable(
            name: "ObservationProject",
            newName: "ObservationDataset");

        migrationBuilder.RenameColumn(
            name: "DatasetOrgId",
            table: "ObservationEntityIndex",
            newName: "CollectionOrgId");

        migrationBuilder.RenameColumn(
            name: "DatasetOrgId",
            table: "Observation",
            newName: "CollectionOrgId");
    }
}

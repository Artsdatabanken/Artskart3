using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// CompleteFilter, opprydding 2 av 2 — fjerner det gamle skjemaet.
///
/// DETTE ER PUNKTET UTEN RETUR. Etter denne finnes kildedataene bare i backup.
/// Kjør den først når release 2 er verifisert i alle miljøer.
///
/// Hva som fjernes:
/// - OrganizationRelation (136 625 713 rader, 5 699 MB inkl. indekser). Institusjon
///   og samling ligger nå som kolonner på Observation, datasett i ObservationDataset.
/// - OrganizationRelationType (1 rad, «Owner»). RelationTypeId bar aldri informasjon.
/// - Observation.InstitutionId, InstitutionCode, CollectionCode med tilhørende
///   indekser. Kodene utledes fra Organization.Code — se ExportColumnRegistry.
///
/// INNHOLDSGUARD, ikke bare rekkefølgeguard.
/// Rekkefølgen (EF kjører sekvensielt og registrerer ikke en migrasjon som feilet)
/// garanterer at BackfillAll *returnerte* suksess. Den sier ingenting om at
/// dataene faktisk er der: en redigert __EFMigrationsHistory, en gjenopprettet
/// backup eller en kjøring mot feil database bryter antakelsen. Kostnaden ved å
/// ta feil er 136M rader som ikke finnes noe annet sted, så innholdet sjekkes
/// eksplisitt her. Sjekkene er indekserte anti-joins og tar sekunder.
///
/// KOLONNENE GJØRES IKKE NOT NULL. Se kommentaren på Observation.InstitutionOrgId:
/// ALTER COLUMN over 61M rader er en size-of-data-operasjon under Sch-M-lås, den
/// feiler på kolonner som er bundet av fremmednøkler, og EFs forberedende
/// «UPDATE ... SET = 0 WHERE IS NULL» ville brutt selve fremmednøkkelen (0 er
/// ingen gyldig Organization.Id). Datakvaliteten sikres av guarden over og av
/// verifiseringen i BackfillAll.
///
/// suppressTransaction på DROP TABLE: å slippe 136M rader er en deallokering som
/// tar tid og loggplass. Inne i migrasjonstransaksjonen kunne loggen ikke avkortes
/// underveis — samme resonnement som for BackfillAll og RemoveInstitutionIndexRows.
/// </summary>
public partial class CleanupCompleteFilterSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ObservationDataset er den kritiske: eneste kilde til datasett-tilknytning
        // etter at OrganizationRelation er borte. E2 ble tidligere ikke verifisert
        // i det hele tatt.
        migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM dbo.OrganizationRelation r
    JOIN dbo.Organization g ON g.Id = r.OrganizationId AND g.OrganizationTypeId = 3
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ObservationDataset d
                      WHERE d.ObservationId = r.ObservationId
                        AND d.DatasetOrgId  = r.OrganizationId))
    THROW 50001, 'CompleteFilter: ObservationDataset er ufullstendig. OrganizationRelation kan ikke slippes - datasett-tilknytningen ville gaatt tapt. Kjoer Scripts/BackfillAll.sql.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.Observation o
    WHERE o.InstitutionOrgId IS NULL
      AND EXISTS (SELECT 1 FROM dbo.OrganizationRelation r
                  JOIN dbo.Organization g ON g.Id = r.OrganizationId
                  WHERE r.ObservationId = o.Id AND g.OrganizationTypeId = 1))
    THROW 50002, 'CompleteFilter: Observation.InstitutionOrgId er ikke ferdig fylt. Kjoer Scripts/BackfillAll.sql.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.Observation o
    WHERE o.CollectionOrgId IS NULL
      AND EXISTS (SELECT 1 FROM dbo.OrganizationRelation r
                  JOIN dbo.Organization g ON g.Id = r.OrganizationId
                  WHERE r.ObservationId = o.Id AND g.OrganizationTypeId = 2))
    THROW 50003, 'CompleteFilter: Observation.CollectionOrgId er ikke ferdig fylt. Kjoer Scripts/BackfillAll.sql.', 1;
");

        migrationBuilder.Sql(
            "DROP TABLE dbo.OrganizationRelation;",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "DROP TABLE dbo.OrganizationRelationType;",
            suppressTransaction: true);

        // Resten er ren metadata og går på millisekunder.
        migrationBuilder.DropIndex(
            name: "IX_Observation_InstitutionCode",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_InstitutionId",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "CollectionCode",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "InstitutionCode",
            table: "Observation");

        migrationBuilder.DropColumn(
            name: "InstitutionId",
            table: "Observation");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Gjenskaper kolonnene, men ikke dataene — og ikke de to tabellene, som er
        // borte for godt. En reell tilbakerulling herfra er gjenoppretting fra
        // backup, ikke en Down-metode som later som.
        migrationBuilder.AddColumn<string>(
            name: "CollectionCode",
            table: "Observation",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InstitutionCode",
            table: "Observation",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InstitutionId",
            table: "Observation",
            type: "nvarchar(25)",
            maxLength: 25,
            nullable: true);
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Oppretter ObservationEntityIndex-tabellen med indekser. Kun skjema — ingen data.
///
/// DATAFYLLINGEN ER FLYTTET UT (august 2026).
/// Migrasjonen inneholdt opprinnelig to INSERT-setninger som fylte tabellen med
/// ~192M rader (områder fra Observation -> Location -> LocationAreas -> Area, og
/// institusjoner fra OrganizationRelation). Begge kjørte uten
/// suppressTransaction, altså som én transaksjon.
///
/// Det er samme konstruksjon som feilet for ObservationTaxonHierarchy: på Azure SQL
/// er loggskriving hardt begrenset per servicenivå, og med alt i én transaksjon kan
/// loggen aldri avkortes underveis. Den kjøringen sprengte både CommandTimeout(1800)
/// og oppstartsgrensen til App Service, og en feil ville i tillegg gitt en like lang
/// rollback. Samme resonnement er dokumentert i Scripts/BackfillAll.sql.
///
/// Radene fylles nå av migrasjonen BackfillAll, som kjører batchvis, uten
/// transaksjon og idempotent (NOT EXISTS). Den setter også inn rader som mangler
/// fordi denne migrasjonen feilet halvveis, så et delvis fylt miljø repareres.
///
/// MERK for miljøer der denne migrasjonen allerede er anvendt (test, lokalt):
/// __EFMigrationsHistory lagrer bare MigrationId og ProductVersion — ingen
/// sjekksum — så endringen her får ingen konsekvens der. Radene ligger allerede
/// inne, og BackfillAll finner ingenting å gjøre.
///
/// Kolonnene som kom til i 20260814125543 har defaultverdier (TaxonGroupId = 0,
/// BasisOfRecordId = 0, HasMediaFiles = 0, RegistrationStatusId = 0). Rader som
/// settes inn av BackfillAll får dermed TaxonGroupId = 0, som er nettopp markøren
/// backfillen av filterkolonnene leter etter. Rekkefølgen går opp av seg selv.
/// </summary>
public partial class AddObservationEntityIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ObservationEntityIndex",
            columns: table => new
            {
                ObservationId = table.Column<int>(type: "int", nullable: false),
                EntityTypeId = table.Column<int>(type: "int", nullable: false),
                EntityId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ObservationEntityIndex", x => new { x.ObservationId, x.EntityTypeId, x.EntityId });
            });

        // Datafyllingen lå her. Se klassekommentaren — den er flyttet til
        // migrasjonen BackfillAll.

        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_Lookup",
            table: "ObservationEntityIndex",
            columns: new[] { "EntityTypeId", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_ObservationEntityIndex_ObservationId",
            table: "ObservationEntityIndex",
            column: "ObservationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ObservationEntityIndex");
    }
}

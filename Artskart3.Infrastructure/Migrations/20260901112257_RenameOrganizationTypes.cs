using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Gir de tre organisasjonstypene navn fra GBIF-vokabularet.
///
/// Fra og til:
///   Id 1: Institution / Institution   ->  Publisher / GBIF-Publisher
///   Id 2: Collection  / Description   ->  Dataset   / GBIF-Dataset
///   Id 3: Dataset     / Dataset       ->  Project   / GBIF-event and Artsops project
///
/// Beskrivelsen for Id 2 var bokstavelig talt «Description» — en plassholder som
/// har ligget der siden tabellen ble opprettet.
///
/// REN TEKSTENDRING, INGEN BETYDNINGSENDRING. Ingen kode slår opp typene på navn;
/// alt bruker ID-ene (LookupController: 2 og 3, LookupRepository: 1). Radene er
/// tre i tallet, så dette går på millisekunder og trenger verken batching eller
/// suppressTransaction.
///
/// MERK EN NAVNEKOLLISJON SOM BLIR SYNLIG HER:
/// Grensesnittet kaller type 2 «Samling» og type 3 «Prosjekt/datasett». Med de nye
/// navnene heter type 2 «Dataset» og type 3 «Project». Filteret som står som
/// «Samling» i sidemenyen filtrerer altså på det GBIF kaller Dataset, og
/// «Prosjekt» filtrerer på Project/event. Selve filtrene treffer riktige rader —
/// de har alltid brukt ID — men etikettene i grensesnittet og språkfilene er ikke
/// oppdatert i denne migrasjonen. Se sidebar.component.html og no.json/en.json.
/// </summary>
public partial class RenameOrganizationTypes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE dbo.OrganizationType SET Name = 'Publisher', Description = 'GBIF-Publisher' WHERE Id = 1;
UPDATE dbo.OrganizationType SET Name = 'Dataset',   Description = 'GBIF-Dataset'   WHERE Id = 2;
UPDATE dbo.OrganizationType SET Name = 'Project',   Description = 'GBIF-event and Artsops project' WHERE Id = 3;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Gjenoppretter nøyaktig det som stod før, plassholderen «Description»
        // inkludert. En Down som «rydder opp» underveis ville ikke vært en
        // tilbakerulling.
        migrationBuilder.Sql(@"
UPDATE dbo.OrganizationType SET Name = 'Institution', Description = 'Institution' WHERE Id = 1;
UPDATE dbo.OrganizationType SET Name = 'Collection',  Description = 'Description' WHERE Id = 2;
UPDATE dbo.OrganizationType SET Name = 'Dataset',     Description = 'Dataset'     WHERE Id = 3;
");
    }
}

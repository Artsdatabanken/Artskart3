using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Synkroniserer modellsnapshotet med defaultene som allerede finnes i databasen.
/// Endrer ingenting i databasen — Up og Down er tomme med vilje.
///
/// HVORFOR
/// AddObservationDenormalisedFilterColumns opprettet kolonnene med rå SQL:
///
///     RegistrationStatusId TINYINT NOT NULL
///         CONSTRAINT DF_Observation_RegistrationStatusId DEFAULT (1),
///     HasMediaFiles        BIT     NOT NULL
///         CONSTRAINT DF_Observation_HasMediaFiles DEFAULT (0)
///
/// Rå SQL er usynlig for snapshotet, så modellen kjente ikke defaultene. Det ble
/// oppdaget av integrasjonstestene: de bygger skjemaet med EnsureCreated() fra
/// modellen, ikke fra migrasjonene, og fikk derfor NOT NULL uten default. Hver
/// INSERT i seed_data.sql som ikke nevner kolonnen feilet — 102 av 104 tester.
///
/// Defaultene er nå deklarert i ArtskartDbContext, og denne migrasjonen bærer den
/// endringen inn i snapshotet slik at has-pending-model-changes blir ren igjen.
///
/// HVORFOR TOM Up
/// EF ville generert AlterColumn for begge kolonnene. Det ville sluppet de navngitte
/// constraintene og lagt på nye med EF-genererte navn — og da ville Down i
/// AddObservationDenormalisedFilterColumns feilet, siden den slipper
/// DF_Observation_RegistrationStatusId og DF_Observation_HasMediaFiles ved navn.
///
/// Databasen har allerede nøyaktig de defaultene modellen nå beskriver. Det er bare
/// snapshotet som var på etterskudd.
/// </summary>
public partial class SyncObservationDenormalisedColumnDefaults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Med vilje tom. Se klassekommentaren.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Med vilje tom. Se klassekommentaren.
    }
}

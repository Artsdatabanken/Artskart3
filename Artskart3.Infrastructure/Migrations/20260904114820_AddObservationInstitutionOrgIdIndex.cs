using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Indeks på Observation.InstitutionOrgId — gjør institusjonsfilteret i
/// listevisningen til et seek i stedet for et fullt tabellskann.
///
/// DateTimeCollected er NØKKELkolonne nummer to, ikke INCLUDE. Listevisningen
/// sorterer nyeste først (SearchRepository), og med denne rekkefølgen kan planen
/// seeke på institusjonen og så gå BAKOVER gjennom datoene innenfor den — ferdig
/// sortert, uten Sort-operator. Er datoen bare INCLUDE, må hele treffmengden
/// sorteres for å plukke 40.
///
/// Forskjellen er ikke marginal. Målt på 61 052 216 rader, institusjon alene,
/// nyeste først, uten og med dato som nøkkelkolonne:
///   Havforskningsinstituttet (480 402 obs)   23 294 ms ->  1 ms
///   Norges miljø- og biovitensk. (149 720)    9 465 ms ->  3 ms
///   Universitetsmuseet i Bergen (602 804)     2 687 ms ->  4 ms
///   Birdlife Norge (29,2M)                    2 120 ms ->  3 ms
///
/// MERK REKKEFØLGEN: sorteringen i koden og denne indeksen hører sammen. Ruller
/// man tilbake denne migrasjonen uten å endre sorteringen, faller listevisningen
/// tilbake til tallene i venstre kolonne.
///
/// INCLUDE-kolonnen koster 462 MB -> 600 MB.
/// </summary>
public partial class AddObservationInstitutionOrgIdIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // suppressTransaction + IF NOT EXISTS av samme grunn som
        // IX_Observation_CatalogNumber: bygget går over 61M rader og forventes å
        // kunne bli avbrutt. Ved en klienttimeout rulles det tilbake, men blir
        // prosessen drept uten at en attention når serveren, kjører CREATE INDEX
        // ofte ferdig likevel. Historikkraden er da ikke skrevet, og neste forsøk
        // ville feilet permanent med «There is already an index named ...».
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Observation_InstitutionOrgId'
                 AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Observation_InstitutionOrgId
    ON dbo.Observation (InstitutionOrgId, DateTimeCollected)
    WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);
END
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_Observation_InstitutionOrgId'
             AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    DROP INDEX IX_Observation_InstitutionOrgId ON dbo.Observation;
END
", suppressTransaction: true);
    }
}

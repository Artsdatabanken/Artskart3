using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Indeks på Observation.InstitutionOrgId — gjør institusjonsfilteret i
/// listevisningen til et seek i stedet for et fullt tabellskann.
///
/// DateTimeCollected er med som INCLUDE-kolonne, ikke nøkkelkolonne. Uten den
/// måtte planen gjøre et key lookup mot klyngeindeksen for hver kandidatrad bare
/// for å lese datoen og teste periodefilteret. Med den evalueres periodepredikatet
/// direkte i indeksen, og oppslagene forsvinner.
///
/// Nøkkelen er fortsatt bare InstitutionOrgId. Datoen kan ikke være nøkkelkolonne:
/// periodefilteret er et OMRÅDEpredikat, og et område på andre nøkkelkolonne ville
/// ødelagt Id-rekkefølgen listevisningen sorterer på.
///
/// Målt på 61 052 216 rader, institusjon + periode 2020-2024:
///   Birdlife Norge (29,2M obs)      2 339 ms -> 936 ms
///   Miljødirektoratet (2,27M obs)   1 924 ms ->   3 ms
///   Naturhistorisk Museum (4,2M)             ->  52 ms
///   GBIF-noder utenfor Norge (4,7M)          ->   5 ms
/// Institusjon uten periode er uendret på 1-11 ms.
///
/// Birdlife blir liggende på ~936 ms fordi institusjonen alene er 48 % av tabellen
/// — selv et rent indeksskann må da lese langt. Alt under ~5M observasjoner faller
/// til tosifrede millisekunder.
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
    ON dbo.Observation (InstitutionOrgId)
    INCLUDE (DateTimeCollected)
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

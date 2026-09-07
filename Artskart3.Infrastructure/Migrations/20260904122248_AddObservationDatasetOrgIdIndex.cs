using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Indeks på Observation.DatasetOrgId — samme grep som
/// 20260904114820_AddObservationInstitutionOrgIdIndex, for samme spørreform.
///
/// DateTimeCollected er med som INCLUDE-kolonne av samme grunn som der: uten den
/// må planen gjøre et key lookup per kandidatrad bare for å teste periodefilteret.
/// Se institusjonsmigrasjonen for hvorfor datoen ikke kan være nøkkelkolonne.
///
/// Målt på 61 052 216 rader, datasett + periode 2020-2024, før INCLUDE:
///   so2-birds (29,2M obs)                 2 364 ms
///   import hos GBIF-noder (4,7M obs)      2 539 ms
///   so2-vascular (4,3M obs)               2 002 ms
///   dnv (2,2M obs)                        2 858 ms
/// Datasettfilteret er skjevere enn institusjon — 1 862 av 1 908 datasett har
/// under 100 000 observasjoner — så dette treffer nesten hvert datasett en bruker
/// kan velge i typeaheaden.
/// </summary>
public partial class AddObservationDatasetOrgIdIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // suppressTransaction + IF NOT EXISTS av samme grunn som
        // institusjonsindeksen: bygget går over 61M rader og forventes å kunne bli
        // avbrutt. Ved en klienttimeout rulles det tilbake, men blir prosessen
        // drept uten at en attention når serveren, kjører CREATE INDEX ofte ferdig
        // likevel. Historikkraden er da ikke skrevet, og neste forsøk ville feilet
        // permanent med «There is already an index named ...».
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Observation_DatasetOrgId'
                 AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Observation_DatasetOrgId
    ON dbo.Observation (DatasetOrgId)
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
           WHERE name = 'IX_Observation_DatasetOrgId'
             AND object_id = OBJECT_ID('dbo.Observation'))
BEGIN
    DROP INDEX IX_Observation_DatasetOrgId ON dbo.Observation;
END
", suppressTransaction: true);
    }
}

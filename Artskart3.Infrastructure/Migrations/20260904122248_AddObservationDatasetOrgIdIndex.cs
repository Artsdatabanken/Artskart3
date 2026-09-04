using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Indeks på Observation.DatasetOrgId — samme grep som
/// 20260904114820_AddObservationInstitutionOrgIdIndex, for samme spørreform.
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

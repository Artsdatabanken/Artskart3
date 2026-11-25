using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller HiddenByAnnotation og tar kolonnen inn i columnstore. Samme kjede som
/// BackfillObservationIdentificationAndOrigin: Observation fra taggen, indekstabellen
/// fra Observation.
///
/// STATISTIKKOPPDATERINGEN ER PÅKREVD
/// 7 267 endringer ligger langt under terskelen for automatisk oppdatering, så den
/// filtrerte indeksen ville beholdt statistikken fra da den var tom.
///
/// COLUMNSTORE SLIPPES OG BYGGES PÅ NYTT
/// Den kan ikke utvides med ALTER. Mens migrasjonen går er områdetellingene ~10x
/// tregere. Bygget tok 13 min sist.
///
/// Kolonnelista skal ikke speiles i Scripts/BackfillAll.sql: det skriptet kjører før
/// kolonnen finnes, og denne migrasjonen bygger uansett indeksen med full liste.
///
/// suppressTransaction: indeksbygget tar langt over timeout-grensen.
/// </summary>
public partial class BackfillObservationHiddenByAnnotation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- Slipp columnstore først -----------------------------------------
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Slipper IX_OEI_Columnstore (bygges paa nytt til slutt)...', 0, 1) WITH NOWAIT;
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;
END
", suppressTransaction: true);

        // --- Observation fra taggen -------------------------------------------
        //
        // Oppfyllbart predikat: en ny kjøring etter avbrudd gjør bare det som gjenstår.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Rows INT, @Msg NVARCHAR(200);

UPDATE o SET o.HiddenByAnnotation = 1
FROM dbo.Observation o
INNER JOIN dbo.ObservationTags ot ON ot.ObservationId = o.Id
WHERE ot.TagId = 7 AND o.HiddenByAnnotation = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Observation.HiddenByAnnotation: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

UPDATE STATISTICS dbo.Observation (IX_Observation_HiddenByAnnotation) WITH FULLSCAN;
", suppressTransaction: true);

        // --- Indekstabellen fra Observation ------------------------------------
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Rows INT, @Msg NVARCHAR(200);

UPDATE idx SET idx.HiddenByAnnotation = 1
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.HiddenByAnnotation = 1 AND idx.HiddenByAnnotation = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Indeks.HiddenByAnnotation: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
", suppressTransaction: true);

        // --- Bygg columnstore med den nye kolonnen ----------------------------
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore med HiddenByAnnotation (10-30 min)...', 0, 1) WITH NOWAIT;

    CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
    ON dbo.ObservationEntityIndex
        (ObservationId, EntityTypeId, EntityId,
         TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
         HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
         SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
         InstitutionOrgId, DatasetOrgId, BehaviorId,
         LocationId, MonthCollected,
         ChangedByAnnotation, Sensitive,
         UncertainIdentification, Introduced,
         HiddenByAnnotation)
    WITH (MAXDOP = 4);

    RAISERROR('IX_OEI_Columnstore opprettet.', 0, 1) WITH NOWAIT;
END
", suppressTransaction: true);

        // --- Verifisering ------------------------------------------------------
        //
        // Begge retninger mot de merkede radene, så kontrollen slipper å sammenligne
        // hele tabellene.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @AvvikKilde INT, @AvvikIndeks INT, @Msg NVARCHAR(500);

SELECT @AvvikKilde =
    (SELECT COUNT(*)
     FROM dbo.ObservationTags ot
     INNER JOIN dbo.Observation o ON o.Id = ot.ObservationId
     WHERE ot.TagId = 7 AND o.HiddenByAnnotation = 0)
  + (SELECT COUNT(*)
     FROM dbo.Observation o
     WHERE o.HiddenByAnnotation = 1
       AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                       WHERE ot.ObservationId = o.Id AND ot.TagId = 7));

SELECT @AvvikIndeks =
    (SELECT COUNT(*)
     FROM dbo.Observation o
     INNER JOIN dbo.ObservationEntityIndex idx ON idx.ObservationId = o.Id
     WHERE o.HiddenByAnnotation = 1 AND idx.HiddenByAnnotation = 0)
  + (SELECT COUNT(*)
     FROM dbo.ObservationEntityIndex idx
     INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
     WHERE idx.HiddenByAnnotation = 1 AND o.HiddenByAnnotation = 0);

IF @AvvikKilde > 0 OR @AvvikIndeks > 0
BEGIN
    SET @Msg = CONCAT(
        'Verifisering FEILET: ', FORMAT(@AvvikKilde,'N0'), ' rader paa Observation stemmer ikke ',
        'med taggen, og ', FORMAT(@AvvikIndeks,'N0'), ' indeksrader stemmer ikke med Observation. ',
        'En ny kjoering retter bare rader som mangler flagget; rader som har det uten grunn maa ',
        'nullstilles manuelt.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - HiddenByAnnotation stemmer hele kjeden.', 0, 1) WITH NOWAIT;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columnstore bygges tilbake uten kolonnen. Kolonnene selv fjernes av forrige
        // migrasjons Down.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;

CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
ON dbo.ObservationEntityIndex
    (ObservationId, EntityTypeId, EntityId,
     TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
     HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
     SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
     InstitutionOrgId, DatasetOrgId, BehaviorId,
     LocationId, MonthCollected,
     ChangedByAnnotation, Sensitive,
     UncertainIdentification, Introduced)
WITH (MAXDOP = 4);
", suppressTransaction: true);
    }
}

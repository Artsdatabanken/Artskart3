using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller ObservationEntityIndex.LocationId og tar kolonnen inn i columnstore.
///
/// De to hører sammen: kolonnen uten columnstore gir ingen gevinst, siden hele
/// poenget er at aggregeringen skal kunne gå i batch mode.
///
/// BATCHVIS OG RESUMERBART
/// 134,7 millioner rader. Vannmerke i dbo.BackfillProgress etter mønsteret fra
/// BackfillAll, så et avbrudd fortsetter der det slapp i stedet for å begynne
/// forfra. Predikatet er oppfyllbart — raden matcher ikke etter oppdateringen —
/// så en ny kjøring gjør bare det som gjenstår.
///
/// COLUMNSTORE SLIPPES OG BYGGES PÅ NYTT
/// En columnstore-indeks kan ikke utvides med ALTER. Den slippes derfor FØR
/// backfillen: uten det ville hver batch skrevet til deltastore, og indeksen
/// måtte bygges om likevel. Rekkefølgen sparer altså ett fullt bygg.
///
/// Konsekvens: mens migrasjonen går, finnes ikke columnstore, og
/// områdetellingene er ~10x tregere i det vinduet. Det var de også under
/// BackfillAll, av samme grunn.
///
/// KOLONNELISTEN MÅ HOLDES I SYNK med Scripts/BackfillAll.sql, som eier
/// indeksen ved en full datafylling. Legges en kolonne til her uten å legges til
/// der, forsvinner den neste gang BackfillAll bygger indeksen.
///
/// MERK at BackfillAll.sql fortsatt skriver CollectionOrgId i sin kolonneliste.
/// Kolonnen heter DatasetOrgId etter migrasjon 20260901124537. Skriptet kjører
/// før omdøpingen på et ferskt miljø og er derfor riktig der, men lista under
/// bruker dagens navn fordi denne migrasjonen alltid kjører etterpå.
///
/// suppressTransaction: backfillen og indeksbygget tar til sammen langt over
/// timeout-grensen. I én transaksjon ville et avbrudd rullet tilbake alt.
/// </summary>
public partial class BackfillObservationEntityIndexLocationId : Migration
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

        // --- Backfill ---------------------------------------------------------
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

IF OBJECT_ID('dbo.BackfillProgress') IS NULL
    CREATE TABLE dbo.BackfillProgress (
        Section         VARCHAR(64) NOT NULL PRIMARY KEY,
        LastCompletedId INT         NOT NULL,
        UpdatedAt       DATETIME2   NOT NULL
            CONSTRAINT DF_BackfillProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

DECLARE @BatchSize INT = 10000000;
DECLARE @MinId INT, @MaxId INT, @CurrentId INT, @BatchEnd INT;
DECLARE @Rows INT, @Total BIGINT = 0;
DECLARE @Msg NVARCHAR(400);
DECLARE @Start DATETIME2 = SYSUTCDATETIME();

SELECT @MinId = MIN(ObservationId), @MaxId = MAX(ObservationId)
FROM dbo.ObservationEntityIndex;

IF @MinId IS NULL
    RAISERROR('ObservationEntityIndex er tom - ingenting aa fylle.', 0, 1) WITH NOWAIT;
ELSE
BEGIN
    SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                                WHERE Section = 'H1_EntityIndexLocationId'), @MinId - 1) + 1;

    IF @CurrentId > @MaxId
        RAISERROR('H1 allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

    WHILE @CurrentId <= @MaxId
    BEGIN
        SET @BatchEnd = @CurrentId + @BatchSize - 1;

        -- Oppfyllbart predikat: treffer bare rader der kilden har en verdi
        -- indeksen mangler, saa raden matcher ikke etter oppdateringen.
        UPDATE idx
        SET idx.LocationId = o.LocationId
        FROM dbo.ObservationEntityIndex idx
        INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND idx.LocationId IS NULL
          AND o.LocationId IS NOT NULL
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
        SET @Total = @Total + @Rows;

        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'H1_EntityIndexLocationId' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(),'HH:mm:ss'), ' | H1 | ',
                          FORMAT(@CurrentId,'N0'), '-', FORMAT(@BatchEnd,'N0'),
                          ' | ', FORMAT(@Rows,'N0'), ' | totalt ', FORMAT(@Total,'N0'),
                          ' | ', DATEDIFF(SECOND,@Start,SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

        SET @CurrentId = @BatchEnd + 1;
    END
END
", suppressTransaction: true);

        // --- Bygg columnstore med den nye kolonnen ----------------------------
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore med LocationId (10-30 min)...', 0, 1) WITH NOWAIT;

    CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
    ON dbo.ObservationEntityIndex
        (ObservationId, EntityTypeId, EntityId,
         TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
         HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
         SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
         InstitutionOrgId, DatasetOrgId, BehaviorId,
         LocationId)
    WITH (MAXDOP = 4);

    RAISERROR('IX_OEI_Columnstore opprettet.', 0, 1) WITH NOWAIT;
END
", suppressTransaction: true);

        // --- Statistikk og verifisering ---------------------------------------
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

-- Kolonnen er ny og tabellen nettopp omskrevet. Uten dette planlegger
-- optimalisereren mot statistikk som ikke kjenner LocationId i det hele tatt.
RAISERROR('Oppdaterer statistikk (10-20 min)...', 0, 1) WITH NOWAIT;
UPDATE STATISTICS dbo.ObservationEntityIndex WITH FULLSCAN;

-- Verifisering: hver indeksrad skal ha samme LocationId som observasjonen sin.
-- Kontrollen leser mot kilden, ikke mot vannmerket, og er oppfyllbar.
DECLARE @Avvik BIGINT;
SELECT @Avvik = COUNT_BIG(*)
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.LocationId IS NOT NULL
  AND (idx.LocationId IS NULL OR idx.LocationId <> o.LocationId);

DECLARE @Msg NVARCHAR(400) = CONCAT('Verifisering: ', FORMAT(@Avvik,'N0'), ' avvikende rader');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

IF @Avvik > 0
BEGIN
    SET @Msg = CONCAT('UFULLSTENDIG: ', FORMAT(@Avvik,'N0'),
                      ' rader mangler LocationId. Nullstill H1_EntityIndexLocationId og kjoer paa nytt.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - LocationId stemmer med Observation.', 0, 1) WITH NOWAIT;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columnstore bygges tilbake uten LocationId. Kolonnen selv fjernes av
        // forrige migrasjons Down.
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
     InstitutionOrgId, DatasetOrgId, BehaviorId)
WITH (MAXDOP = 4);

DELETE FROM dbo.BackfillProgress WHERE Section = 'H1_EntityIndexLocationId';
", suppressTransaction: true);
    }
}

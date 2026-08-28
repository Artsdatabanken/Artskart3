-- ============================================================================
-- Backfill av taksonhierarki-kolonner på ObservationEntityIndex
-- Kjøres manuelt i SSMS etter at migrasjonen har lagt til kolonnene.
--
-- Kolonner som oppdateres:
--   SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId
--
-- Data hentes fra ObservationTaxonHierarchy (rang 22, 19, 15, 11).
-- Bruker ObservationId-range for jevn fremdrift.
-- Skriptet kan trygt kjøres flere ganger — allerede oppdaterte rader hoppes over.
-- ============================================================================

SET NOCOUNT ON;

-- Deaktiver columnstore-indeksen under backfill.
-- En UPDATE mot columnstore markerer den gamle raden i delete-bitmappen og setter
-- inn en ny versjon i delta store — over 190M rader blir det svært dyrt.
--
-- Indeksen opprettes av migrasjon 20260820140154 og skal normalt finnes her.
-- Sjekken er defensiv: mangler den, er noe galt med migrasjonstilstanden.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Deaktiverer IX_OEI_Columnstore...', 0, 1) WITH NOWAIT;
    ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex DISABLE;
    RAISERROR('Indeks deaktivert.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('ADVARSEL: IX_OEI_Columnstore finnes ikke. Har migrasjonene kjoert?', 16, 1) WITH NOWAIT;

RAISERROR('---', 0, 1) WITH NOWAIT;

DECLARE @BatchSize INT = 500000;
DECLARE @MinId INT;
DECLARE @MaxId INT;
DECLARE @CurrentId INT;
DECLARE @BatchEnd INT;
DECLARE @RowsUpdated INT;
DECLARE @TotalUpdated BIGINT = 0;
DECLARE @StartTime DATETIME2 = SYSUTCDATETIME();
DECLARE @Msg NVARCHAR(500);

-- Finn range av ObservationId i hierarkitabellen
SELECT @MinId = MIN(ObservationId), @MaxId = MAX(ObservationId)
FROM dbo.ObservationTaxonHierarchy;

SET @CurrentId = @MinId;

SET @Msg = CONCAT('ObservationId-range: ', FORMAT(@MinId, 'N0'), ' - ', FORMAT(@MaxId, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Estimerte batcher: ', CEILING(CAST(@MaxId - @MinId AS FLOAT) / @BatchSize));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
RAISERROR('---', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    UPDATE idx
    SET idx.SpeciesTaxonId  = h.SpeciesTaxonId,
        idx.GenusTaxonId    = h.GenusTaxonId,
        idx.FamilyTaxonId   = h.FamilyTaxonId,
        idx.OrderTaxonId    = h.OrderTaxonId
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId
      AND idx.ObservationId <= @BatchEnd
      AND idx.OrderTaxonId IS NULL
      AND (h.SpeciesTaxonId IS NOT NULL OR h.GenusTaxonId IS NOT NULL
        OR h.FamilyTaxonId IS NOT NULL OR h.OrderTaxonId IS NOT NULL);

    SET @RowsUpdated = @@ROWCOUNT;
    SET @TotalUpdated = @TotalUpdated + @RowsUpdated;

    SET @Msg = CONCAT(
        FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | ',
        'Range: ', FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'), ' | ',
        'Oppdatert: ', FORMAT(@RowsUpdated, 'N0'), ' rader | ',
        'Totalt: ', FORMAT(@TotalUpdated, 'N0'), ' | ',
        'Tid: ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), 's'
    );
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

RAISERROR('---', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Backfill ferdig! Totalt oppdatert: ', FORMAT(@TotalUpdated, 'N0'), ' rader paa ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), ' sekunder.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- Gjenoppbygg columnstore-indeksen.
-- En deaktivert indeks kan ikke reaktiveres — REBUILD er eneste vei tilbake.
-- Dette tar 10-30 minutter paa 192M rader.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Gjenoppbygger IX_OEI_Columnstore (10-30 min)...', 0, 1) WITH NOWAIT;
    ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex REBUILD;
    RAISERROR('Indeks gjenoppbygget!', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('ADVARSEL: IX_OEI_Columnstore mangler - omraadetellinger blir 10x tregere.', 16, 1) WITH NOWAIT;

RAISERROR('Ferdig!', 0, 1) WITH NOWAIT;

-- Kontroller at indeksen ble bygget med fulle rowgroups.
-- Forvent ~180+ COMPRESSED rowgroups med snitt naer 1 048 576 rader.
SELECT
    state_desc                                    AS Tilstand,
    COUNT(*)                                      AS RowGroups,
    SUM(total_rows)                               AS Rader,
    AVG(total_rows)                               AS SnittRaderPerGruppe,
    CAST(SUM(size_in_bytes) / 1024.0 / 1024 AS DECIMAL(10,1)) AS StoerrelseMB
FROM sys.dm_db_column_store_row_group_physical_stats
WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex')
GROUP BY state_desc;

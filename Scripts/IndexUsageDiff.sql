-- ============================================================================
-- Indeksbruk på ObservationEntityIndex — før/etter-måling
--
-- Brukes sammen med Scripts/PerfTestAreaCounts.ps1 for å se hvilke indekser
-- som faktisk brukes, og dermed hvilke som eventuelt kan fjernes.
--
-- Bruk:
--   1. Kjør DEL 1 (lager baseline)
--   2. Kjør PerfTestAreaCounts.ps1
--   3. Kjør DEL 2 (viser differansen)
--
-- MERK: Tellerne i sys.dm_db_index_usage_stats akkumuleres fra SQL Server
-- startet, og nullstilles ved restart. Derfor måler vi differansen, ikke
-- totalen — totalen blander inn all tidligere aktivitet.
--
-- Baseline lagres i en permanent tabell (ikke #temp) slik at den overlever
-- at du bytter query-vindu i SSMS.
-- ============================================================================


-- ---------------------------------------------------------------------------
-- DEL 1: Lag baseline (kjør før ytelsestesten)
-- ---------------------------------------------------------------------------

IF OBJECT_ID('dbo.__IndexUsageBaseline') IS NOT NULL
    DROP TABLE dbo.__IndexUsageBaseline;

SELECT
    i.name        AS IndexName,
    i.index_id    AS IndexId,
    i.type_desc   AS IndexType,
    ISNULL(s.user_seeks,   0) AS UserSeeks,
    ISNULL(s.user_scans,   0) AS UserScans,
    ISNULL(s.user_lookups, 0) AS UserLookups,
    SYSUTCDATETIME()          AS CapturedAt
INTO dbo.__IndexUsageBaseline
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats s
       ON s.object_id = i.object_id
      AND s.index_id  = i.index_id
      AND s.database_id = DB_ID()
WHERE i.object_id = OBJECT_ID('dbo.ObservationEntityIndex');

SELECT 'Baseline lagret' AS Status, COUNT(*) AS Indekser FROM dbo.__IndexUsageBaseline;
GO


-- ---------------------------------------------------------------------------
-- DEL 2: Vis differansen (kjør etter ytelsestesten)
-- ---------------------------------------------------------------------------

-- Differansen beregnes i en CTE. T-SQL kan ikke slå opp SELECT-alias inne i et
-- ORDER BY-uttrykk når spørringen har GROUP BY, og størrelsen hentes som subquery
-- slik at vi slipper GROUP BY helt.
WITH Bruk AS (
    SELECT
        b.IndexName,
        b.IndexType,
        b.IndexId,
        ISNULL(s.user_seeks,   0) - b.UserSeeks   AS SeeksSiden,
        ISNULL(s.user_scans,   0) - b.UserScans   AS ScansSiden,
        ISNULL(s.user_lookups, 0) - b.UserLookups AS LookupsSiden,
        (SELECT CAST(SUM(p.used_page_count) * 8.0 / 1024 AS DECIMAL(10,1))
         FROM sys.dm_db_partition_stats p
         WHERE p.object_id = OBJECT_ID('dbo.ObservationEntityIndex')
           AND p.index_id  = b.IndexId)          AS StoerrelseMB
    FROM dbo.__IndexUsageBaseline b
    LEFT JOIN sys.dm_db_index_usage_stats s
           ON s.object_id   = OBJECT_ID('dbo.ObservationEntityIndex')
          AND s.index_id    = b.IndexId
          AND s.database_id = DB_ID()
)
SELECT
    IndexName,
    IndexType,
    SeeksSiden,
    ScansSiden,
    LookupsSiden,
    StoerrelseMB,
    CASE
        WHEN IndexId = 1 THEN 'Clustered - er selve tabellen, kan ikke fjernes'
        WHEN SeeksSiden + ScansSiden + LookupsSiden = 0
            THEN 'UBRUKT i denne maalingen - kandidat for fjerning'
        ELSE 'I bruk - behold'
    END AS Vurdering
FROM Bruk
ORDER BY SeeksSiden + ScansSiden DESC;
GO


-- ---------------------------------------------------------------------------
-- Tilstand på columnstore-indeksen
-- Kjør etter import/backfill for å se om REORGANIZE trengs.
-- ---------------------------------------------------------------------------

SELECT
    state_desc                                    AS Tilstand,
    COUNT(*)                                      AS RowGroups,
    SUM(total_rows)                               AS Rader,
    SUM(deleted_rows)                             AS SlettedeRader,
    CAST(100.0 * SUM(deleted_rows) / NULLIF(SUM(total_rows), 0) AS DECIMAL(5,2)) AS ProsentSlettet,
    AVG(total_rows)                               AS SnittRaderPerGruppe,
    CAST(SUM(size_in_bytes) / 1024.0 / 1024 AS DECIMAL(10,1)) AS StoerrelseMB
FROM sys.dm_db_column_store_row_group_physical_stats
WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex')
GROUP BY state_desc;

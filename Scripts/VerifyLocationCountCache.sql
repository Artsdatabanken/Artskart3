/*
    Verifiserer lokasjonsbufferen mot tellingen.

    Bufferen skal gi NØYAKTIG det GetLocationsAsync ville talt — lokasjon for
    lokasjon, tall for tall. Dette skriptet sjekker det på ekte data, i motsetning
    til integrasjonstestene som kjører mot noen titalls seedede rader.

    Leser bare. Trygt å kjøre når som helst.

    Tre nivåer av sjekk:
      1. Globale invarianter for den ufiltrerte dimensjonen — billig, fanger at
         hele bygget er skjevt.
      2. Full sammenligning mot tellingen for konkrete filtre i et ekte
         kartutsnitt — det er denne som faktisk beviser noe.
      3. Fordeling og størrelse, til rapportering.
*/

SET NOCOUNT ON;

DECLARE @MinX INT = 191504, @MaxX INT = 376757,    -- Oslo-utsnittet slik frontenden
        @MinY INT = 6987008, @MaxY INT = 7089626;  -- sender det paa maks zoom

PRINT '=== 1. Globale invarianter for dimensjon 0 (ufiltrert) ===';

-- Antall lokasjoner i bufferen skal vaere antall distinkte lokasjoner tellingen
-- ser. Avviker de, mangler bufferen lokasjoner eller har funnet paa noen.
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.LocationCountCacheLevel1 WHERE DimensionId = 0) AS BufferLokasjoner,
    (SELECT COUNT_BIG(DISTINCT LocationId) FROM dbo.ObservationEntityIndex
     WHERE EntityTypeId <> 101 AND LocationId IS NOT NULL)                        AS TeltLokasjoner;

-- Summen av antallene skal vaere antall distinkte observasjoner med lokasjon.
-- Er den hoeyere, teller bufferen indeksrader i stedet for observasjoner - den
-- feilen ville vaert usynlig per lokasjon, men lyser her.
SELECT
    (SELECT SUM(CAST(ObservationCount AS BIGINT)) FROM dbo.LocationCountCacheLevel1 WHERE DimensionId = 0) AS BufferSum,
    (SELECT COUNT_BIG(DISTINCT ObservationId) FROM dbo.ObservationEntityIndex
     WHERE EntityTypeId <> 101 AND LocationId IS NOT NULL)                                                 AS TeltSum;

PRINT '';
PRINT '=== 2. Full sammenligning i Oslo-utsnittet ===';

-- Ufiltrert.
WITH telt AS (
    SELECT i.LocationId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    JOIN dbo.Location l ON l.Id = i.LocationId
    WHERE i.EntityTypeId <> 101 AND i.LocationId IS NOT NULL
      AND l.East BETWEEN @MinX AND @MaxX AND l.North BETWEEN @MinY AND @MaxY
    GROUP BY i.LocationId
),
buffret AS (
    SELECT LocationId, ObservationCount AS Antall
    FROM dbo.LocationCountCacheLevel1
    WHERE DimensionId = 0
      AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY
)
SELECT 'Ufiltrert' AS Tilfelle,
       (SELECT COUNT_BIG(*) FROM telt)     AS TeltRader,
       (SELECT COUNT_BIG(*) FROM buffret)  AS BufferRader,
       (SELECT COUNT_BIG(*) FROM telt t FULL JOIN buffret b ON b.LocationId = t.LocationId
        WHERE t.LocationId IS NULL OR b.LocationId IS NULL OR t.Antall <> b.Antall) AS Avvik;

-- Den stoerste taksongruppen. Velges dynamisk slik at skriptet ikke er avhengig
-- av en hardkodet id som kan endre seg.
DECLARE @TaxonGroupId INT = (
    SELECT TOP 1 TaxonGroupId FROM dbo.ObservationEntityIndex
    WHERE EntityTypeId <> 101 AND LocationId IS NOT NULL
    GROUP BY TaxonGroupId ORDER BY COUNT_BIG(*) DESC);

DECLARE @TaxonGroupDim TINYINT = 7;

WITH telt AS (
    SELECT i.LocationId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    JOIN dbo.Location l ON l.Id = i.LocationId
    WHERE i.EntityTypeId <> 101 AND i.LocationId IS NOT NULL
      AND i.TaxonGroupId = @TaxonGroupId
      AND l.East BETWEEN @MinX AND @MaxX AND l.North BETWEEN @MinY AND @MaxY
    GROUP BY i.LocationId
),
buffret AS (
    SELECT LocationId, ObservationCount AS Antall
    FROM dbo.LocationCountCacheLevel1
    WHERE DimensionId = @TaxonGroupDim AND BucketId = @TaxonGroupId
      AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY
)
SELECT 'Taksongruppe' AS Tilfelle, @TaxonGroupId AS Verdi,
       (SELECT COUNT_BIG(*) FROM telt)     AS TeltRader,
       (SELECT COUNT_BIG(*) FROM buffret)  AS BufferRader,
       (SELECT COUNT_BIG(*) FROM telt t FULL JOIN buffret b ON b.LocationId = t.LocationId
        WHERE t.LocationId IS NULL OR b.LocationId IS NULL OR t.Antall <> b.Antall) AS Avvik;

-- Den stoerste kategorien.
DECLARE @CategoryId INT = (
    SELECT TOP 1 CategoryId FROM dbo.ObservationEntityIndex
    WHERE EntityTypeId <> 101 AND LocationId IS NOT NULL AND CategoryId IS NOT NULL
    GROUP BY CategoryId ORDER BY COUNT_BIG(*) DESC);

DECLARE @CategoryDim TINYINT = 5;

WITH telt AS (
    SELECT i.LocationId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    JOIN dbo.Location l ON l.Id = i.LocationId
    WHERE i.EntityTypeId <> 101 AND i.LocationId IS NOT NULL
      AND i.CategoryId = @CategoryId
      AND l.East BETWEEN @MinX AND @MaxX AND l.North BETWEEN @MinY AND @MaxY
    GROUP BY i.LocationId
),
buffret AS (
    SELECT LocationId, ObservationCount AS Antall
    FROM dbo.LocationCountCacheLevel1
    WHERE DimensionId = @CategoryDim AND BucketId = @CategoryId
      AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY
)
SELECT 'Kategori' AS Tilfelle, @CategoryId AS Verdi,
       (SELECT COUNT_BIG(*) FROM telt)     AS TeltRader,
       (SELECT COUNT_BIG(*) FROM buffret)  AS BufferRader,
       (SELECT COUNT_BIG(*) FROM telt t FULL JOIN buffret b ON b.LocationId = t.LocationId
        WHERE t.LocationId IS NULL OR b.LocationId IS NULL OR t.Antall <> b.Antall) AS Avvik;

-- Geografi. Den farligste: boettene er flerverdi og slaas opp i medlemstabellen,
-- og en observasjon i flere valgte omraader skal telles EN gang.
DECLARE @KommuneEntityId INT = (
    SELECT TOP 1 EntityId FROM dbo.ObservationEntityIndex
    WHERE EntityTypeId = 1 AND LocationId IS NOT NULL
    GROUP BY EntityId ORDER BY COUNT_BIG(*) DESC);

DECLARE @GeografiDim TINYINT = 12;
DECLARE @Medlem INT = 1 * 1000000 + @KommuneEntityId;

WITH boetter AS (
    SELECT BucketId FROM dbo.AreaCountCacheBucketMember
    WHERE DimensionId = @GeografiDim AND MemberId = @Medlem
),
telt AS (
    SELECT i.LocationId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    JOIN dbo.Location l ON l.Id = i.LocationId
    WHERE i.EntityTypeId <> 101 AND i.LocationId IS NOT NULL
      AND l.East BETWEEN @MinX AND @MaxX AND l.North BETWEEN @MinY AND @MaxY
      AND EXISTS (SELECT 1 FROM dbo.ObservationEntityIndex k
                  WHERE k.ObservationId = i.ObservationId
                    AND k.EntityTypeId = 1 AND k.EntityId = @KommuneEntityId)
    GROUP BY i.LocationId
),
buffret AS (
    SELECT c.LocationId, SUM(c.ObservationCount) AS Antall
    FROM dbo.LocationCountCacheLevel1 c
    JOIN boetter b ON b.BucketId = c.BucketId
    WHERE c.DimensionId = @GeografiDim
      AND c.East BETWEEN @MinX AND @MaxX AND c.North BETWEEN @MinY AND @MaxY
    GROUP BY c.LocationId
)
SELECT 'Geografi (kommune)' AS Tilfelle, @KommuneEntityId AS Verdi,
       (SELECT COUNT_BIG(*) FROM telt)     AS TeltRader,
       (SELECT COUNT_BIG(*) FROM buffret)  AS BufferRader,
       (SELECT COUNT_BIG(*) FROM telt t FULL JOIN buffret b ON b.LocationId = t.LocationId
        WHERE t.LocationId IS NULL OR b.LocationId IS NULL OR t.Antall <> b.Antall) AS Avvik;

PRINT '';
PRINT '=== 3. Fordeling og stoerrelse ===';

SELECT DimensionId,
       COUNT_BIG(*)                 AS Rader,
       COUNT_BIG(DISTINCT BucketId) AS Boetter
FROM dbo.LocationCountCacheLevel1
GROUP BY DimensionId ORDER BY DimensionId;

SELECT SUM(p.rows)                                     AS Rader,
       CAST(SUM(a.total_pages) * 8.0 / 1024 AS DECIMAL(10,1)) AS TotaltMB,
       CAST(SUM(a.used_pages)  * 8.0 / 1024 AS DECIMAL(10,1)) AS BruktMB
FROM sys.partitions p
JOIN sys.allocation_units a ON a.container_id = p.hobt_id
WHERE p.object_id = OBJECT_ID('dbo.LocationCountCacheLevel1') AND p.index_id IN (0, 1);

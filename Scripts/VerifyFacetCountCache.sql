/*
    Verifiserer fasett-bufferen mot aggregeringen.

    Bufferen skal gi NØYAKTIG det en live aggregering ville gitt — bøtte for bøtte,
    tall for tall. Dette skriptet sjekker det på ekte data, i motsetning til
    integrasjonstestene som kjører mot noen titalls seedede rader.

    Leser bare. Trygt å kjøre når som helst.

    Fire nivåer av sjekk:
      1. Globale invarianter — billige, fanger at hele bygget er skjevt.
      2. Nivå 0 og 1 mot aggregeringen for tomt filter.
      3. Nivå 2 mot aggregeringen for konkrete filtre — det er denne som beviser noe.
      4. Fordeling og størrelse, til rapportering.

    MERK at aggregeringen her teller COUNT(DISTINCT i.ObservationId) mot
    indekstabellen, altså den dyre formen bufferen finnes for å unngå. Hvert tilfelle
    tar titalls sekunder. Det er med vilje: sammenligningsgrunnlaget skal være
    uavhengig av #obs-dedupliseringen byggeskriptet bruker, ellers ville skriptet
    bare bekreftet seg selv.
*/

SET NOCOUNT ON;

DECLARE @NullBucket INT = -2147483648;
DECLARE @Maske NVARCHAR(400) = N'CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16';

PRINT '=== 0. Tilstand ===';

SELECT Id, SchemaVersion, Status, BuiltAt, SourceRows, DurationSeconds
FROM dbo.FacetCountCacheState;

-- Radantallet bufferen ble bygget fra mot det som ligger der nå. Spriker de, er
-- bufferen eldre enn dataene, og oppslaget bruker den ikke — da er resten av dette
-- skriptet uinteressant.
SELECT
    (SELECT SourceRows FROM dbo.FacetCountCacheState WHERE Id = 1) AS ByggetFraRader,
    (SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS BIGINT) FROM sys.partitions p
     WHERE p.object_id = OBJECT_ID('dbo.ObservationEntityIndex') AND p.index_id IN (0, 1)) AS RaderNaa;

PRINT '';
PRINT '=== 1. Globale invarianter ===';

-- Nivå 0 summert over alle masker skal være antall distinkte observasjoner i
-- indekstabellen. Er den høyere, teller bufferen indeksrader i stedet for
-- observasjoner — den feilen er usynlig per bøtte, men lyser her.
SELECT 'Nivaa 0 sum' AS Sjekk,
    (SELECT SUM(CAST(ObservationCount AS BIGINT)) FROM dbo.FacetCountCacheLevel0) AS Buffer,
    (SELECT COUNT_BIG(DISTINCT ObservationId) FROM dbo.ObservationEntityIndex)    AS Aggregert;

-- Hver NOT NULL-dimensjon skal summere til det samme som nivå 0: hver observasjon
-- ligger i nøyaktig én bøtte. Regstatus (2), funntype (4) og taksongruppe (7) er
-- NOT NULL i indekstabellen; de andre har en NULL-bøtte som ikke bygges, så de
-- summerer lavere, og det er riktig.
SELECT 'Nivaa 1 sum per NOT NULL-dimensjon' AS Sjekk, DimensionId,
       SUM(CAST(ObservationCount AS BIGINT)) AS Buffer
FROM dbo.FacetCountCacheLevel1
WHERE DimensionId IN (2, 4, 7)
GROUP BY DimensionId;

SELECT 'Skal vaere lik de tre over' AS Sjekk,
       COUNT_BIG(DISTINCT ObservationId) AS Aggregert
FROM dbo.ObservationEntityIndex;

-- Ingen NULL-bøtte skal finnes: byggeskriptet filtrerer dem bort, og et oppslag
-- som fant dem ville summert «uten verdi» inn i et intervall.
SELECT 'NULL-boetter (skal vaere 0)' AS Sjekk,
    (SELECT COUNT_BIG(*) FROM dbo.FacetCountCacheLevel1 WHERE BucketId = @NullBucket) AS Nivaa1,
    (SELECT COUNT_BIG(*) FROM dbo.FacetCountCacheLevel2
     WHERE BucketA = @NullBucket OR BucketB = @NullBucket)                            AS Nivaa2;

-- Ingen bøtte skal ha null observasjoner. En slik rad er dødvekt og betyr at
-- byggeskriptet grupperte på noe det ikke burde.
SELECT 'Rader med antall 0 (skal vaere 0)' AS Sjekk,
    (SELECT COUNT_BIG(*) FROM dbo.FacetCountCacheLevel0 WHERE ObservationCount = 0) AS Nivaa0,
    (SELECT COUNT_BIG(*) FROM dbo.FacetCountCacheLevel1 WHERE ObservationCount = 0) AS Nivaa1,
    (SELECT COUNT_BIG(*) FROM dbo.FacetCountCacheLevel2 WHERE ObservationCount = 0) AS Nivaa2;

-- Alle tolv dimensjonene skal finnes på nivå 1, og alle 66 par på nivå 2. Mangler
-- noen, er bygget avbrutt eller en dimensjonsrad har falt ut av #dim.
SELECT 'Dekning' AS Sjekk,
    (SELECT COUNT(DISTINCT DimensionId) FROM dbo.FacetCountCacheLevel1)     AS Dimensjoner,
    (SELECT COUNT(DISTINCT DimensionPairId) FROM dbo.FacetCountCacheLevel2) AS Par;

PRINT '';
PRINT '=== 2. Nivaa 1 mot aggregeringen, tomt filter ===';

-- Kategorifasetten med tomt filter: nivå 1 summert over masker, mot en live
-- aggregering. Dette er tallet sidebaren vil vise når ingenting er valgt.
WITH aggregert AS (
    SELECT ISNULL(i.CategoryId, @NullBucket) AS BucketId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    GROUP BY ISNULL(i.CategoryId, @NullBucket)
),
buffret AS (
    SELECT BucketId, SUM(CAST(ObservationCount AS BIGINT)) AS Antall
    FROM dbo.FacetCountCacheLevel1 WHERE DimensionId = 5 GROUP BY BucketId
)
SELECT 'Kategori, tomt filter' AS Tilfelle,
       SUM(CASE WHEN ISNULL(a.Antall, 0) <> ISNULL(b.Antall, 0) THEN 1 ELSE 0 END) AS Avvik,
       COUNT(*) AS Boetter
FROM aggregert a
FULL JOIN buffret b ON b.BucketId = a.BucketId
WHERE a.BucketId <> @NullBucket OR a.BucketId IS NULL;

-- Taksongruppefasetten, samme form. To dimensjoner er nok for denne sjekken:
-- feiler bygget, feiler det likt for alle.
WITH aggregert AS (
    SELECT i.TaxonGroupId AS BucketId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    GROUP BY i.TaxonGroupId
),
buffret AS (
    SELECT BucketId, SUM(CAST(ObservationCount AS BIGINT)) AS Antall
    FROM dbo.FacetCountCacheLevel1 WHERE DimensionId = 7 GROUP BY BucketId
)
SELECT 'Taksongruppe, tomt filter' AS Tilfelle,
       SUM(CASE WHEN ISNULL(a.Antall, 0) <> ISNULL(b.Antall, 0) THEN 1 ELSE 0 END) AS Avvik,
       COUNT(*) AS Boetter
FROM aggregert a
FULL JOIN buffret b ON b.BucketId = a.BucketId;

PRINT '';
PRINT '=== 3. Nivaa 2 mot aggregeringen, ett aktivt filter ===';

-- Kategorifasetten med taksongruppe 8 (fugler) valgt. Dette er det vanligste
-- tilfellet: brukeren har valgt én ting og ser tallene for en annen gruppe.
--
-- Pargruppe-id-en for (5, 7) regnes ut her på samme måte som C# og byggeskriptet
-- gjør det, framfor å skrives inn som en literal — en literal her ville bare
-- bekreftet seg selv.
DECLARE @Par57 TINYINT = (
    SELECT PairId FROM (
        SELECT a.Id AS IdA, b.Id AS IdB,
               CAST(ROW_NUMBER() OVER (ORDER BY a.Id, b.Id) AS TINYINT) AS PairId
        FROM (VALUES (2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13)) a(Id)
        JOIN (VALUES (2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13)) b(Id) ON b.Id > a.Id
    ) q WHERE IdA = 5 AND IdB = 7);

SELECT 'Pargruppe (5,7)' AS Sjekk, @Par57 AS PairId;

WITH aggregert AS (
    SELECT ISNULL(i.CategoryId, @NullBucket) AS BucketId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    WHERE i.TaxonGroupId = 8
    GROUP BY ISNULL(i.CategoryId, @NullBucket)
),
buffret AS (
    SELECT BucketA AS BucketId, SUM(CAST(ObservationCount AS BIGINT)) AS Antall
    FROM dbo.FacetCountCacheLevel2
    WHERE DimensionPairId = @Par57 AND BucketB = 8
    GROUP BY BucketA
)
SELECT 'Kategori gitt taksongruppe 8' AS Tilfelle,
       SUM(CASE WHEN ISNULL(a.Antall, 0) <> ISNULL(b.Antall, 0) THEN 1 ELSE 0 END) AS Avvik,
       COUNT(*) AS Boetter
FROM aggregert a
FULL JOIN buffret b ON b.BucketId = a.BucketId
WHERE ISNULL(a.BucketId, 0) <> @NullBucket;

-- Samme par, motsatt vei: taksongruppefasetten med kategori 9 valgt. Fanger at
-- BucketA og BucketB er byttet om.
WITH aggregert AS (
    SELECT i.TaxonGroupId AS BucketId, COUNT(DISTINCT i.ObservationId) AS Antall
    FROM dbo.ObservationEntityIndex i
    WHERE i.CategoryId = 9
    GROUP BY i.TaxonGroupId
),
buffret AS (
    SELECT BucketB AS BucketId, SUM(CAST(ObservationCount AS BIGINT)) AS Antall
    FROM dbo.FacetCountCacheLevel2
    WHERE DimensionPairId = @Par57 AND BucketA = 9
    GROUP BY BucketB
)
SELECT 'Taksongruppe gitt kategori 9' AS Tilfelle,
       SUM(CASE WHEN ISNULL(a.Antall, 0) <> ISNULL(b.Antall, 0) THEN 1 ELSE 0 END) AS Avvik,
       COUNT(*) AS Boetter
FROM aggregert a
FULL JOIN buffret b ON b.BucketId = a.BucketId;

PRINT '';
PRINT '=== 3b. Maskefasett mot aggregeringen ===';

-- «Med bilde» uten andre filtre: nivå 0 med HasMediaFiles-biten satt.
SELECT 'Med bilde, tomt filter' AS Tilfelle,
    (SELECT SUM(CAST(ObservationCount AS BIGINT)) FROM dbo.FacetCountCacheLevel0
     WHERE (Mask & 16) = 16)                                             AS Buffer,
    (SELECT COUNT_BIG(DISTINCT ObservationId) FROM dbo.ObservationEntityIndex
     WHERE HasMediaFiles = 1)                                            AS Aggregert;

-- «Uten bilde» — negasjonen, som er den som lett blir feil.
SELECT 'Uten bilde, tomt filter' AS Tilfelle,
    (SELECT SUM(CAST(ObservationCount AS BIGINT)) FROM dbo.FacetCountCacheLevel0
     WHERE (Mask & 16) = 0)                                              AS Buffer,
    (SELECT COUNT_BIG(DISTINCT ObservationId) FROM dbo.ObservationEntityIndex
     WHERE HasMediaFiles = 0)                                            AS Aggregert;

-- «Med bilde» gitt taksongruppe 8: nivå 1 summert over den ene dimensjonens bøtte.
SELECT 'Med bilde gitt taksongruppe 8' AS Tilfelle,
    (SELECT SUM(CAST(ObservationCount AS BIGINT)) FROM dbo.FacetCountCacheLevel1
     WHERE DimensionId = 7 AND BucketId = 8 AND (Mask & 16) = 16)        AS Buffer,
    (SELECT COUNT_BIG(DISTINCT ObservationId) FROM dbo.ObservationEntityIndex
     WHERE TaxonGroupId = 8 AND HasMediaFiles = 1)                       AS Aggregert;

PRINT '';
PRINT '=== 4. Fordeling og stoerrelse ===';

SELECT 'Nivaa 0' AS Tabell, COUNT_BIG(*) AS Rader FROM dbo.FacetCountCacheLevel0
UNION ALL SELECT 'Nivaa 1', COUNT_BIG(*) FROM dbo.FacetCountCacheLevel1
UNION ALL SELECT 'Nivaa 2', COUNT_BIG(*) FROM dbo.FacetCountCacheLevel2;

SELECT t.name AS Tabell,
       CAST(SUM(a.total_pages) * 8.0 / 1024 AS DECIMAL(10, 1)) AS MB,
       MAX(p.data_compression_desc) AS Komprimering
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
WHERE t.name LIKE 'FacetCountCache%'
GROUP BY t.name
ORDER BY t.name;

-- De tyngste parene, til sammenligning med områdebufferens fordeling.
SELECT TOP 10 DimensionPairId, COUNT_BIG(*) AS Rader
FROM dbo.FacetCountCacheLevel2
GROUP BY DimensionPairId
ORDER BY COUNT_BIG(*) DESC;

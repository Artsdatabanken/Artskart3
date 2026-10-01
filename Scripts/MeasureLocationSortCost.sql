/*
    Bryter ned hva et buffret lokasjonsoppslag med 100 000 treff faktisk bruker
    tiden paa.

    Leser bare. Trygt aa kjoere — men IKKE samtidig med perf-suiten, siden begge
    laster samme database og resultatene blir upaalitelige.

    NEDBRYTNINGEN
    Fire varianter som bygger paa hverandre, saa hvert steg isoleres av
    differansen til det forrige:

      A  COUNT(*)                     skanning av bufferen
      B  TOP 100000, uten ORDER BY    + materialisering av 100 000 rader
      C  TOP 100000, med ORDER BY     + sortering
      D  C + join mot Location        + koordinatoppslag

      skanning       = A
      materialisering = B - A
      sortering      = C - B
      koordinatjoin  = D - C

    Serialiseringen ligger utenfor SQL og maales som API-tid minus D.

    FEM GJENTAKELSER PER VARIANT. Enkeltmaalinger paa denne databasen varierer
    med flere hundre millisekunder, og det er nok til aa snu en konklusjon.
    Minimum er mer robust enn snittet naar stoyen bare gaar en vei, saa begge
    rapporteres.
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- oslo-utsnittet slik perf-suiten definerer det: 967 649 lokasjoner totalt,
-- 483 272 av dem med observasjoner, og vi returnerer 100 000.
DECLARE @MinX INT = 170566, @MaxX INT = 355820, @MinY INT = 6599473, @MaxY INT = 6702091;
DECLARE @Top INT = 100000;

-- Dimensjon 0 = ufiltrert. Dimensjon 7 = taksongruppe, det vanligste
-- ett-filter-tilfellet; 65 av de 118 kallene som treffer taket har ett filter.
DECLARE @Dim TINYINT, @Bucket INT, @Sak VARCHAR(30);

CREATE TABLE #r (Sak VARCHAR(30), Variant CHAR(1), Ms INT, Rader INT);
DECLARE @t DATETIME2, @i INT, @n INT;

DECLARE c CURSOR LOCAL FAST_FORWARD FOR
SELECT * FROM (VALUES
    ('ufiltrert',    CAST(0 AS TINYINT), 0),
    ('taksongruppe', CAST(7 AS TINYINT),
        (SELECT TOP 1 BucketId FROM dbo.LocationCountCacheLevel1
         WHERE DimensionId = 7 GROUP BY BucketId ORDER BY COUNT_BIG(*) DESC))
) v(a,b,c);

OPEN c; FETCH NEXT FROM c INTO @Sak, @Dim, @Bucket;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @i = 0;
    WHILE @i < 5
    BEGIN
        SET @i += 1;

        -- A: bare skanningen
        SET @t = SYSUTCDATETIME();
        SELECT @n = COUNT_BIG(*)
        FROM dbo.LocationCountCacheLevel1
        WHERE DimensionId = @Dim AND BucketId = @Bucket
          AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY;
        INSERT #r VALUES (@Sak, 'A', DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()), @n);

        -- B: + materialisering, uten sortering
        SET @t = SYSUTCDATETIME();
        SELECT TOP(@Top) LocationId, ObservationCount
        INTO #b
        FROM dbo.LocationCountCacheLevel1
        WHERE DimensionId = @Dim AND BucketId = @Bucket
          AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY;
        INSERT #r SELECT @Sak, 'B', DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()), COUNT(*) FROM #b;
        DROP TABLE #b;

        -- C: + sortering. Ordrett som LookupAsync.
        SET @t = SYSUTCDATETIME();
        SELECT TOP(@Top) LocationId, ObservationCount
        INTO #c
        FROM dbo.LocationCountCacheLevel1
        WHERE DimensionId = @Dim AND BucketId = @Bucket
          AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY
        ORDER BY ObservationCount DESC, LocationId;
        INSERT #r SELECT @Sak, 'C', DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()), COUNT(*) FROM #c;
        DROP TABLE #c;

        -- D: + koordinatjoin. Hele spoerringen tjenesten sender.
        SET @t = SYSUTCDATETIME();
        SELECT q.LocationId, l.Latitude, l.Longitude, q.ObservationCount
        INTO #d
        FROM (
            SELECT TOP(@Top) LocationId, ObservationCount
            FROM dbo.LocationCountCacheLevel1
            WHERE DimensionId = @Dim AND BucketId = @Bucket
              AND East BETWEEN @MinX AND @MaxX AND North BETWEEN @MinY AND @MaxY
            ORDER BY ObservationCount DESC, LocationId
        ) q
        JOIN dbo.Location l ON l.Id = q.LocationId;
        INSERT #r SELECT @Sak, 'D', DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()), COUNT(*) FROM #d;
        DROP TABLE #d;
    END

    FETCH NEXT FROM c INTO @Sak, @Dim, @Bucket;
END
CLOSE c; DEALLOCATE c;

SELECT Sak, Variant,
       MIN(Ms) AS MinMs, AVG(Ms) AS SnittMs, MAX(Ms) AS MaksMs, MAX(Rader) AS Rader
FROM #r GROUP BY Sak, Variant ORDER BY Sak, Variant;

-- Differansene, regnet paa minimum for aa unngaa at stoy i ett steg
-- tilskrives det neste.
WITH m AS (SELECT Sak, Variant, MIN(Ms) AS Ms FROM #r GROUP BY Sak, Variant)
SELECT a.Sak,
       a.Ms                        AS Skanning,
       b.Ms - a.Ms                 AS Materialisering,
       c.Ms - b.Ms                 AS Sortering,
       d.Ms - c.Ms                 AS Koordinatjoin,
       d.Ms                        AS SumSql
FROM m a
JOIN m b ON b.Sak = a.Sak AND b.Variant = 'B'
JOIN m c ON c.Sak = a.Sak AND c.Variant = 'C'
JOIN m d ON d.Sak = a.Sak AND d.Variant = 'D'
WHERE a.Variant = 'A';

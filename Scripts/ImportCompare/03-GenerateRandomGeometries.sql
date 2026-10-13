/* ============================================================================
   03-GenerateRandomGeometries.sql
   Genererer et tilfeldig punkt inne i destinasjonskommunen for hver sensitiv
   post, og skriver det inn i den skrubbede JSON-en.

   >>> KJØRES ETTER STEG 3 I 01-ScrubSensitiveCoordinates.sql <<<
   >>> FORUTSETTER AT 02-AssignDestination.sql ER KJØRT <<<

   ---------------------------------------------------------------------------
   HVORFOR UTM OG IKKE LAT/LON

   Kommunepolygonene ligger i UTM33 (SRID 32633). SQL Server kan ikke
   reprojisere UTM -> WGS84, og en egen invers projeksjon i T-SQL ville vært
   nok en feilkilde.

   Løsningen er å skrive UTMsone/UTMnord/UTMost i stedet for Latitude/Longitude.
   ImportGeometry håndterer dette direkte:

       if (record.UTMsone == 33 && record.UTMnord > 0) { ... }

   Importen gjør altså transformasjonen selv, likt i begge løsninger.

   Bivirkning: disse postene følger UTM-grenen i ImportGeometry i stedet for
   lat/lon-grenen. For paritetsriggen er det uproblematisk — begge løsninger
   leser samme post — og det gir faktisk bedre dekning av UTM-grenen. Men
   lat/lon-grenen blir ikke testet av nettopp disse postene.

   ---------------------------------------------------------------------------
   DETERMINISTISK

   Punktet trekkes med CHECKSUM over (ProxyRowId, forsøksnummer), ikke RAND().
   En ny kjøring gir derfor nøyaktig samme punkter, og gullsettet forblir
   reproduserbart.
============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: kjor kun mot en lokal kopi av cachen.', 16, 1);
    SET NOEXEC ON;
END

IF OBJECT_ID('dbo._GeoKommune') IS NULL
BEGIN
    RAISERROR('STOPP: _GeoKommune mangler. Kjør 02-AssignDestination.sql først.', 16, 1);
    SET NOEXEC ON;
END

IF EXISTS (SELECT 1 FROM dbo._ScrubTargets WHERE DestKommuneFid IS NULL)
BEGIN
    RAISERROR('STOPP: noen poster mangler destinasjon. Kjør 02-AssignDestination.sql ferdig.', 16, 1);
    SET NOEXEC ON;
END

IF EXISTS (SELECT 1 FROM dbo._ScrubTargets WHERE ScrubbedAt IS NULL)
BEGIN
    RAISERROR('STOPP: noen poster er ikke skrubbet. Kjør steg 3 i 01-skriptet først.', 16, 1);
    SET NOEXEC ON;
END


/* --- 1. Trekk punkt inne i kommunepolygonet ------------------------------
   Forkastingsmetode: trekk i kommunens omsluttende rektangel, behold treff
   som ligger inne i polygonet. Kommuner er stort sett kompakte, så de fleste
   treffer på første eller andre forsøk.
-------------------------------------------------------------------------- */
DECLARE @forsok INT = 0, @igjen INT, @maksForsok INT = 300;

SELECT @igjen = COUNT(*) FROM dbo._ScrubTargets WHERE DestEast IS NULL;
RAISERROR('=== Trekker punkter for %d poster ===', 0, 1, @igjen) WITH NOWAIT;

WHILE @forsok < @maksForsok AND @igjen > 0
BEGIN
    SET @forsok = @forsok + 1;

    ;WITH kand AS (
        SELECT t.ProxyRowId, k.g,
               k.MinX + (k.MaxX - k.MinX) *
                   (ABS(CHECKSUM(CAST(t.ProxyRowId AS VARCHAR(20)) + 'x' + CAST(@forsok AS VARCHAR(4))))
                    % 1000000) / 1000000.0 AS x,
               k.MinY + (k.MaxY - k.MinY) *
                   (ABS(CHECKSUM(CAST(t.ProxyRowId AS VARCHAR(20)) + 'y' + CAST(@forsok AS VARCHAR(4))))
                    % 1000000) / 1000000.0 AS y
        FROM dbo._ScrubTargets t
        JOIN dbo._GeoKommune k ON k.Fid = t.DestKommuneFid
        WHERE t.DestEast IS NULL
    )
    UPDATE t
    SET DestEast  = CAST(ROUND(c.x, 0) AS INT),
        DestNorth = CAST(ROUND(c.y, 0) AS INT)
    FROM dbo._ScrubTargets t
    JOIN kand c ON c.ProxyRowId = t.ProxyRowId
    WHERE c.g.STContains(geometry::Point(c.x, c.y, 32633)) = 1;

    SELECT @igjen = COUNT(*) FROM dbo._ScrubTargets WHERE DestEast IS NULL;

    IF @forsok % 10 = 0 OR @igjen = 0
        RAISERROR('  forsøk %d: %d igjen', 0, 1, @forsok, @igjen) WITH NOWAIT;
END

IF @igjen > 0
    RAISERROR('MERK: %d poster fikk ikke punkt innenfor %d forsøk. Se spørringen under.',
              10, 1, @igjen, @maksForsok) WITH NOWAIT;

SELECT TOP 20 ProxyRowId, DestKommuneNavn, DestFylkeNavn
FROM dbo._ScrubTargets WHERE DestEast IS NULL;
GO


/* --- 2. Skriv punktet inn i JSON-en --------------------------------------
   Setter UTMsone/UTMnord/UTMost. CoordinateUncertaintyInMeters settes til 25,
   slik at postene får en realistisk presisjon å maskeres fra — 0 eller
   manglende presisjon ville sendt dem ned en annen gren i ImportGeometry.
-------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: feil database.', 16, 1); SET NOEXEC ON;
END

IF COL_LENGTH('dbo._ScrubTargets','GeometryWrittenAt') IS NULL
    ALTER TABLE dbo._ScrubTargets ADD GeometryWrittenAt DATETIME2 NULL;
GO

SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#b') IS NOT NULL DROP TABLE #b;
CREATE TABLE #b (ProxyRowId INT PRIMARY KEY, E INT, N INT);

DECLARE @Batch INT = 2000, @N INT = 1, @Sum INT = 0;

RAISERROR('=== Skriver koordinater inn i JSON ===', 0, 1) WITH NOWAIT;

WHILE @N > 0
BEGIN
    DELETE FROM #b;
    INSERT INTO #b (ProxyRowId, E, N)
    SELECT TOP (@Batch) ProxyRowId, DestEast, DestNorth
    FROM dbo._ScrubTargets
    WHERE DestEast IS NOT NULL AND GeometryWrittenAt IS NULL
    ORDER BY ProxyRowId;

    SET @N = @@ROWCOUNT;
    IF @N = 0 BREAK;

    BEGIN TRAN;

    UPDATE p
    SET SerializedObject = COMPRESS(j4.v)
    FROM dbo.DarwinCoreProxies p
    JOIN #b b ON b.ProxyRowId = p.Id
    CROSS APPLY (VALUES (CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)))) j0(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j0.v, '$.UTMsone', 33)))    j1(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j1.v, '$.UTMnord', b.N)))   j2(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j2.v, '$.UTMost',  b.E)))   j3(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j3.v, '$.CoordinateUncertaintyInMeters', N'25'))) j4(v);

    UPDATE t SET GeometryWrittenAt = SYSUTCDATETIME()
    FROM dbo._ScrubTargets t JOIN #b b ON b.ProxyRowId = t.ProxyRowId;

    COMMIT;

    SET @Sum = @Sum + @N;
    RAISERROR('  skrevet %d (totalt %d)', 0, 1, @N, @Sum) WITH NOWAIT;
END

DROP TABLE #b;
GO


/* --- 3. Verifisering ----------------------------------------------------- */
SET NOCOUNT ON;

;WITH sjekk AS (
    SELECT t.ProxyRowId, t.DestEast, t.DestNorth, t.DestKommuneFid,
           CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)) AS j
    FROM dbo._ScrubTargets t
    JOIN dbo.DarwinCoreProxies p ON p.Id = t.ProxyRowId
)
SELECT
    COUNT_BIG(*)                                                                  AS Kontrollerte,
    SUM(CASE WHEN JSON_VALUE(j,'$.Latitude')     IS NOT NULL THEN 1 ELSE 0 END)   AS LatitudeIgjen,
    SUM(CASE WHEN JSON_VALUE(j,'$.Longitude')    IS NOT NULL THEN 1 ELSE 0 END)   AS LongitudeIgjen,
    SUM(CASE WHEN JSON_VALUE(j,'$.FootprintWkt') IS NOT NULL THEN 1 ELSE 0 END)   AS FootprintIgjen,
    SUM(CASE WHEN TRY_CAST(JSON_VALUE(j,'$.UTMost') AS INT) <> DestEast
              OR TRY_CAST(JSON_VALUE(j,'$.UTMnord') AS INT) <> DestNorth
             THEN 1 ELSE 0 END)                                                   AS FeilKoordinat,
    SUM(CASE WHEN TRY_CAST(JSON_VALUE(j,'$.UTMsone') AS INT) <> 33 THEN 1 ELSE 0 END) AS FeilSone
FROM sjekk;

-- Alle kolonner unntatt Kontrollerte skal være 0.

-- Punktene skal ligge inne i den tildelte kommunen:
SELECT COUNT_BIG(*) AS UtenforKommunen
FROM dbo._ScrubTargets t
JOIN dbo._GeoKommune k ON k.Fid = t.DestKommuneFid
WHERE t.DestEast IS NOT NULL
  AND k.g.STContains(geometry::Point(t.DestEast, t.DestNorth, 32633)) = 0;

-- Fordeling, til kontroll
SELECT DestFylkeNavn, COUNT_BIG(*) AS Poster
FROM dbo._ScrubTargets GROUP BY DestFylkeNavn ORDER BY COUNT_BIG(*) DESC;

SET NOEXEC OFF;
GO

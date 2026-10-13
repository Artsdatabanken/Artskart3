/* ============================================================================
   02-AssignDestination.sql
   Velger en destinasjonskommune for hver sensitiv post, i et fylke som IKKE
   grenser til fylket posten faktisk ligger i.

   >>> KJØRES MELLOM STEG 2 OG STEG 3 I 01-ScrubSensitiveCoordinates.sql <<<

   Rekkefølgen er ikke valgfri. For å velge et fylke punktet ikke skal havne i,
   må vi vite hvilket fylke det ligger i nå — og den informasjonen finnes bare i
   den opprinnelige koordinaten, som steg 3 sletter.

   StateProvince kan ikke brukes til dette: det er fritekst fra kilden, ofte tom,
   engelsk, eller med fylkesnavn fra før sammenslåingen i 2020.

   ---------------------------------------------------------------------------
   HVA SOM LAGRES

   Bare DESTINASJONEN. Opphavsfylket brukes underveis og skrives aldri til disk,
   så arbeidstabellen inneholder ingen stedsinformasjon om de ekte funnene.

   ---------------------------------------------------------------------------
   KOORDINATPROJEKSJON I T-SQL

   Postene kan ha koordinat som UTM33 eller som WGS84 lat/lon. Fylkespolygonene
   ligger i UTM33 (SRID 32633), og SQL Server kan ikke reprojisere.

   Skriptet implementerer derfor Transverse Mercator forover (WGS84 -> UTM33).
   Validert mot 200 000 lokaliteter i indeksbasen som har begge deler lagret:
       gjennomsnittlig avvik 0,37 m, største avvik 38 m.
   Det er rikelig når spørsmålet er «hvilket fylke», der fylkene er titalls km.
============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: kjor kun mot en lokal kopi av cachen.', 16, 1);
    SET NOEXEC ON;
END

-- >>> TILPASS: navnet på indeksbasen som har Area-tabellen <<<
DECLARE @IndexDb SYSNAME = N'Artskart3IndexProdLikeTestMigrations';


/* --- Kolonner for destinasjonen ------------------------------------------ */
IF COL_LENGTH('dbo._ScrubTargets','DestKommuneFid') IS NULL
    ALTER TABLE dbo._ScrubTargets ADD
        DestKommuneFid  NVARCHAR(50) NULL,
        DestKommuneNavn NVARCHAR(200) NULL,
        DestFylkeNavn   NVARCHAR(200) NULL,
        DestEast        INT NULL,
        DestNorth       INT NULL;
GO


/* --- Hent geografi lokalt fra indeksbasen -------------------------------- */
SET NOCOUNT ON;
DECLARE @IndexDb SYSNAME = N'Artskart3IndexProdLikeTestMigrations';   -- samme verdi som over
DECLARE @sql NVARCHAR(MAX);

IF OBJECT_ID('dbo._GeoFylke') IS NOT NULL DROP TABLE dbo._GeoFylke;
IF OBJECT_ID('dbo._GeoKommune') IS NOT NULL DROP TABLE dbo._GeoKommune;

SET @sql = N'
SELECT Fid, Name, WktPolygon.MakeValid() AS g,
       WktPolygon.STEnvelope().STPointN(1).STX AS MinX,
       WktPolygon.STEnvelope().STPointN(1).STY AS MinY,
       WktPolygon.STEnvelope().STPointN(3).STX AS MaxX,
       WktPolygon.STEnvelope().STPointN(3).STY AS MaxY,
       WktPolygon.STCentroid().STX AS CX, WktPolygon.STCentroid().STY AS CY
INTO dbo._GeoFylke
FROM ' + QUOTENAME(@IndexDb) + N'.dbo.Area WHERE AreaTypeId = 2 AND IsCurrent = 1;

SELECT Fid, Name, ParentFid, WktPolygon.MakeValid() AS g,
       WktPolygon.STEnvelope().STPointN(1).STX AS MinX,
       WktPolygon.STEnvelope().STPointN(1).STY AS MinY,
       WktPolygon.STEnvelope().STPointN(3).STX AS MaxX,
       WktPolygon.STEnvelope().STPointN(3).STY AS MaxY
INTO dbo._GeoKommune
FROM ' + QUOTENAME(@IndexDb) + N'.dbo.Area WHERE AreaTypeId = 1 AND IsCurrent = 1;';

EXEC sp_executesql @sql;

ALTER TABLE dbo._GeoFylke ALTER COLUMN Fid NVARCHAR(50) NOT NULL;
ALTER TABLE dbo._GeoFylke ADD PRIMARY KEY (Fid);
CREATE INDEX IX_GeoKommune_Parent ON dbo._GeoKommune (ParentFid);

-- Naboskap mellom fylker. 15 x 15, så dette er trivielt.
IF OBJECT_ID('dbo._GeoNabo') IS NOT NULL DROP TABLE dbo._GeoNabo;
SELECT a.Fid AS Fid, b.Fid AS NaboFid
INTO dbo._GeoNabo
FROM dbo._GeoFylke a JOIN dbo._GeoFylke b
  ON a.Fid <> b.Fid AND a.g.STIntersects(b.g) = 1;
CREATE INDEX IX_GeoNabo ON dbo._GeoNabo (Fid, NaboFid);

SELECT (SELECT COUNT(*) FROM dbo._GeoFylke)   AS Fylker,
       (SELECT COUNT(*) FROM dbo._GeoKommune) AS Kommuner,
       (SELECT COUNT(*) FROM dbo._GeoNabo)    AS Naboforhold;
GO


/* --- Tildel destinasjon --------------------------------------------------
   For hver post:
     1. les opprinnelig koordinat (UTM33 direkte, ellers projiser lat/lon)
     2. finn OPPHAVSFYLKER som alle fylker hvis omsluttende rektangel dekker
        punktet. Dette er en bevisst overtilnærming — den gir 1-3 fylker og
        utvider dermed eksklusjonen, aldri omvendt
     3. ekskluder opphavsfylkene OG alle deres naboer
     4. velg deterministisk blant resten; er den tom, velg fylket med fjernest
        sentroide
     5. velg deterministisk en kommune i det fylket

   Valgene er deterministiske (CHECKSUM over ProxyRowId), så en ny kjøring gir
   samme fordeling.
-------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: feil database.', 16, 1); SET NOEXEC ON;
END

DECLARE @Batch INT = 5000, @N INT = 1, @Sum INT = 0;
DECLARE @e2 FLOAT = 0.00669437999014, @a FLOAT = 6378137.0, @k0 FLOAT = 0.9996;

RAISERROR('=== Tildeler destinasjoner ===', 0, 1) WITH NOWAIT;

WHILE @N > 0
BEGIN
    IF OBJECT_ID('tempdb..#felt') IS NOT NULL DROP TABLE #felt;
    IF OBJECT_ID('tempdb..#pos')  IS NOT NULL DROP TABLE #pos;

    -- Dekomprimer én gang per post og trekk ut koordinatfeltene
    SELECT TOP (@Batch)
           t.ProxyRowId,
           TRY_CAST(JSON_VALUE(j.v,'$.Latitude')  AS FLOAT) AS lat,
           TRY_CAST(JSON_VALUE(j.v,'$.Longitude') AS FLOAT) AS lon,
           TRY_CAST(JSON_VALUE(j.v,'$.UTMnord')   AS FLOAT) AS un,
           TRY_CAST(JSON_VALUE(j.v,'$.UTMost')    AS FLOAT) AS ue,
           TRY_CAST(JSON_VALUE(j.v,'$.UTMsone')   AS INT)   AS uz
    INTO #felt
    FROM dbo._ScrubTargets t
    JOIN dbo.DarwinCoreProxies p ON p.Id = t.ProxyRowId
    CROSS APPLY (VALUES (CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)))) j(v)
    WHERE t.DestKommuneFid IS NULL
    ORDER BY t.ProxyRowId;

    IF (SELECT COUNT(*) FROM #felt) = 0 BREAK;

    -- UTM33 har forrang. Ellers Transverse Mercator forover fra WGS84.
    SELECT f.ProxyRowId,
           CASE WHEN f.uz = 33 AND f.un > 0 THEN f.ue ELSE tm.px END AS ox,
           CASE WHEN f.uz = 33 AND f.un > 0 THEN f.un ELSE tm.py END AS oy
    INTO #pos
    FROM #felt f
    OUTER APPLY (
        SELECT 500000 + @k0*Nu*(AA + (1-T2+C2)*POWER(AA,3)/6
                     + (5-18*T2+T2*T2+72*C2-58*ep2)*POWER(AA,5)/120) AS px,
               @k0*(M + Nu*T*(AA*AA/2 + (5-T2+9*C2+4*C2*C2)*POWER(AA,4)/24
                     + (61-58*T2+T2*T2+600*C2-330*ep2)*POWER(AA,6)/720)) AS py
        FROM (
            SELECT phi, ep2, Nu, T, AA, T2, C2,
                   @a*((1-@e2/4-3*POWER(@e2,2)/64-5*POWER(@e2,3)/256)*phi
                      -(3*@e2/8+3*POWER(@e2,2)/32+45*POWER(@e2,3)/1024)*SIN(2*phi)
                      +(15*POWER(@e2,2)/256+45*POWER(@e2,3)/1024)*SIN(4*phi)
                      -(35*POWER(@e2,3)/3072)*SIN(6*phi)) AS M
            FROM (
                SELECT phi, dl,
                       @e2/(1-@e2)                                  AS ep2,
                       @a/SQRT(1-@e2*POWER(SIN(phi),2))             AS Nu,
                       TAN(phi)                                     AS T,
                       dl*COS(phi)                                  AS AA,
                       POWER(TAN(phi),2)                            AS T2,
                       (@e2/(1-@e2))*POWER(COS(phi),2)              AS C2
                FROM (SELECT RADIANS(f.lat) AS phi, RADIANS(f.lon - 15.0) AS dl) q
            ) c
        ) m
        WHERE f.lat BETWEEN 50 AND 82 AND f.lon BETWEEN -15 AND 45
    ) tm;

    SET @N = (SELECT COUNT(*) FROM #pos);

    ;WITH opphav AS (          -- overtilnærmet: alle fylker hvis bbox dekker punktet
        SELECT DISTINCT p.ProxyRowId, fy.Fid
        FROM #pos p JOIN dbo._GeoFylke fy
          ON p.ox BETWEEN fy.MinX AND fy.MaxX AND p.oy BETWEEN fy.MinY AND fy.MaxY
    ),
    utelatt AS (               -- opphav + alle naboer til opphav
        SELECT ProxyRowId, Fid FROM opphav
        UNION
        SELECT o.ProxyRowId, n.NaboFid FROM opphav o JOIN dbo._GeoNabo n ON n.Fid = o.Fid
    ),
    kand AS (
        SELECT p.ProxyRowId, fy.Fid, fy.Name,
               ROW_NUMBER() OVER (PARTITION BY p.ProxyRowId ORDER BY fy.Fid) AS rn,
               COUNT(*)    OVER (PARTITION BY p.ProxyRowId)                  AS n
        FROM #pos p CROSS JOIN dbo._GeoFylke fy
        WHERE NOT EXISTS (SELECT 1 FROM utelatt u
                          WHERE u.ProxyRowId = p.ProxyRowId AND u.Fid = fy.Fid)
    ),
    fjernest AS (              -- nødløsning: brukes bare hvis kandidatsettet er tomt
        SELECT p.ProxyRowId, fy.Fid, fy.Name,
               ROW_NUMBER() OVER (PARTITION BY p.ProxyRowId
                   ORDER BY POWER(fy.CX-p.ox,2)+POWER(fy.CY-p.oy,2) DESC) AS rn
        FROM #pos p CROSS JOIN dbo._GeoFylke fy
    ),
    valgtFylke AS (
        SELECT p.ProxyRowId,
               COALESCE(k.Fid,  fj.Fid)  AS FylkeFid,
               COALESCE(k.Name, fj.Name) AS FylkeNavn
        FROM #pos p
        LEFT JOIN kand k
               ON k.ProxyRowId = p.ProxyRowId
              AND k.rn = 1 + ABS(CHECKSUM(CAST(p.ProxyRowId AS VARCHAR(20)) + '|fylke')) % k.n
        LEFT JOIN fjernest fj ON fj.ProxyRowId = p.ProxyRowId AND fj.rn = 1
    ),
    kommune AS (
        SELECT v.ProxyRowId, v.FylkeNavn, kk.Fid, kk.Name,
               ROW_NUMBER() OVER (PARTITION BY v.ProxyRowId ORDER BY kk.Fid) AS rn,
               COUNT(*)    OVER (PARTITION BY v.ProxyRowId)                  AS n
        FROM valgtFylke v JOIN dbo._GeoKommune kk ON kk.ParentFid = v.FylkeFid
    )
    UPDATE t
    SET DestKommuneFid  = km.Fid,
        DestKommuneNavn = km.Name,
        DestFylkeNavn   = km.FylkeNavn
    FROM dbo._ScrubTargets t
    JOIN kommune km
      ON km.ProxyRowId = t.ProxyRowId
     AND km.rn = 1 + ABS(CHECKSUM(CAST(t.ProxyRowId AS VARCHAR(20)) + '|kommune')) % km.n;

    SET @Sum = @Sum + @@ROWCOUNT;
    RAISERROR('  tildelt %d (totalt %d)', 0, 1, @N, @Sum) WITH NOWAIT;
END

SELECT DestFylkeNavn, COUNT_BIG(*) AS Poster
FROM dbo._ScrubTargets GROUP BY DestFylkeNavn ORDER BY COUNT_BIG(*) DESC;

-- Skal være 0. Er den det, kan steg 3 i 01-skriptet kjøres.
SELECT COUNT_BIG(*) AS UtenDestinasjon FROM dbo._ScrubTargets WHERE DestKommuneFid IS NULL;

SET NOEXEC OFF;
GO

/* ============================================================================
   02b-VerifyDestination.sql
   Kontrollerer at hver tildelte destinasjon ligger i et fylke som verken ER
   eller GRENSER TIL fylket posten faktisk ligger i.

   >>> KJØRES ETTER 02, FØR 01 STEG 3 <<<

   Dette er det eneste tidspunktet kontrollen er mulig. Steg 3 sletter de
   opprinnelige koordinatene, og da finnes det ingen måte å etterprøve regelen.

   Skriptet er skrivefritt — det leser bare.

   Alle tre tallene i sluttrapporten skal være 0. Er de det, kan steg 3 kjøres.
============================================================================ */

SET NOCOUNT ON;

IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: kjor mot den lokale cache-kopien.', 16, 1);
    SET NOEXEC ON;
END

IF OBJECT_ID('dbo._GeoFylke') IS NULL OR OBJECT_ID('dbo._GeoNabo') IS NULL
BEGIN
    RAISERROR(N'STOPP: _GeoFylke/_GeoNabo mangler. Kjor 02-AssignDestination.sql forst.', 16, 1);
    SET NOEXEC ON;
END

DECLARE @e2 FLOAT = 0.00669437999014, @a FLOAT = 6378137.0, @k0 FLOAT = 0.9996;

/* --- Opprinnelig posisjon, samme utregning som i 02 --------------------- */
IF OBJECT_ID('tempdb..#v') IS NOT NULL DROP TABLE #v;

SELECT t.ProxyRowId, t.DestKommuneFid, t.DestFylkeNavn, k.ParentFid AS DestFylkeFid,
       CASE WHEN f.uz = 33 AND f.un > 0 THEN f.ue ELSE tm.px END AS ox,
       CASE WHEN f.uz = 33 AND f.un > 0 THEN f.un ELSE tm.py END AS oy
INTO #v
FROM dbo._ScrubTargets t
JOIN dbo._GeoKommune k ON k.Fid = t.DestKommuneFid
JOIN dbo.DarwinCoreProxies p ON p.Id = t.ProxyRowId
CROSS APPLY (VALUES (CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)))) j(v)
CROSS APPLY (VALUES (
        TRY_CAST(JSON_VALUE(j.v,'$.Latitude')  AS FLOAT),
        TRY_CAST(JSON_VALUE(j.v,'$.Longitude') AS FLOAT),
        TRY_CAST(JSON_VALUE(j.v,'$.UTMnord')   AS FLOAT),
        TRY_CAST(JSON_VALUE(j.v,'$.UTMost')    AS FLOAT),
        TRY_CAST(JSON_VALUE(j.v,'$.UTMsone')   AS INT)
    )) f(lat, lon, un, ue, uz)
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
        FROM (SELECT phi, dl, @e2/(1-@e2) AS ep2,
                     @a/SQRT(1-@e2*POWER(SIN(phi),2)) AS Nu, TAN(phi) AS T,
                     dl*COS(phi) AS AA, POWER(TAN(phi),2) AS T2,
                     (@e2/(1-@e2))*POWER(COS(phi),2) AS C2
              FROM (SELECT RADIANS(f.lat) AS phi, RADIANS(f.lon - 15.0) AS dl) q) c
    ) m
    WHERE f.lat BETWEEN 50 AND 82 AND f.lon BETWEEN -15 AND 45
) tm;

/* --- Kontroll ------------------------------------------------------------
   Opphav bestemmes med omsluttende rektangel, samme overtilnærming som i 02.
   Det gjør kontrollen strengere enn tildelingen, aldri mildere.
------------------------------------------------------------------------- */
-- MERK: flaggene beregnes per rad FØRST, i en egen tabell. En aggregatfunksjon
-- kan ikke inneholde en subquery, så SUM(CASE WHEN EXISTS(...)) feiler ved
-- binding — og SET PARSEONLY fanger det ikke opp.
IF OBJECT_ID('tempdb..#flagg') IS NOT NULL DROP TABLE #flagg;

SELECT v.ProxyRowId, v.ox, v.oy, v.DestFylkeNavn, v.DestKommuneFid,
       CASE WHEN EXISTS (
            SELECT 1 FROM dbo._GeoFylke o
            WHERE v.ox BETWEEN o.MinX AND o.MaxX AND v.oy BETWEEN o.MinY AND o.MaxY
              AND o.Fid = v.DestFylkeFid)
       THEN 1 ELSE 0 END AS ErOpphav,
       CASE WHEN EXISTS (
            SELECT 1 FROM dbo._GeoFylke o
            JOIN dbo._GeoNabo n ON n.Fid = o.Fid AND n.NaboFid = v.DestFylkeFid
            WHERE v.ox BETWEEN o.MinX AND o.MaxX AND v.oy BETWEEN o.MinY AND o.MaxY)
       THEN 1 ELSE 0 END AS GrenserTilOpphav,
       CASE WHEN v.ox IS NULL OR v.oy IS NULL THEN 1 ELSE 0 END AS UtenKoordinat
INTO #flagg
FROM #v v;

SELECT COUNT_BIG(*)          AS Kontrollerte,
       SUM(ErOpphav)         AS DestErOpphav,
       SUM(GrenserTilOpphav) AS DestGrenserTilOpphav,
       -- ikke en feil: regelen er ikke anvendbar uten opprinnelig koordinat,
       -- og enhver destinasjon er da trygg. Tas med som opplysning.
       SUM(UtenKoordinat)    AS UtenOpprinneligKoordinat
FROM #flagg;

/* --- Fordeling, til øyekontroll ----------------------------------------- */
SELECT DestFylkeNavn, COUNT_BIG(*) AS Poster
FROM dbo._ScrubTargets GROUP BY DestFylkeNavn ORDER BY COUNT_BIG(*) DESC;

/* --- Eksempler, hvis noe skulle slå ut --------------------------------- */
SELECT TOP 20 ProxyRowId, CAST(ox AS INT) AS OpphavEast, CAST(oy AS INT) AS OpphavNorth,
       DestFylkeNavn, DestKommuneFid, ErOpphav, GrenserTilOpphav
FROM #flagg
WHERE ErOpphav = 1 OR GrenserTilOpphav = 1;

DROP TABLE #flagg;
DROP TABLE #v;
SET NOEXEC OFF;
GO

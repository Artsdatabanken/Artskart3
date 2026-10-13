-- ============================================================================
-- RelabelGeometrySrid — merker alle geometrier i indeksbasen om fra
-- EPSG:32633 (WGS 84 / UTM 33N) til EPSG:25833 (ETRS89 / UTM 33N).
--
-- IDENTISK MED MIGRASJONEN RelabelGeometrySridTo25833. Endres den ene, må den
-- andre endres med. Migrasjonen er det som kjører i pipeline; dette skriptet
-- finnes for manuelle kjøringer og reparasjon.
--
-- HVORFOR: for geometry-typen reprojiserer SQL Server aldri. SRID er bare en
-- merkelapp, og en romlig operasjon mellom to ulike SRID-er returnerer NULL —
-- ikke en feilmelding. Location, Area og SensitiveObservationData er merket
-- 32633, mens Maskeringsruter_*-tabellene er merket 25833. Enhver
-- sammenligning mellom de to settene gir derfor NULL, og ser ut som «ingen
-- overlapp». Det er denne merkingen som hindrer både maskeringsrutene
-- (sensitive arter) og et utsnittsfilter på geometrien i å virke i det hele
-- tatt.
--
-- 25833 er målet fordi spatial_ref_sys i basen inneholder 25833 og ikke 32633,
-- geometry_columns har 25833 for maskeringsrutene, og API, frontend og
-- SearchConstants.DefaultEpsgCode bruker 25833 gjennomgående. Det er bare
-- lagringen som avviker.
--
-- OMMERKING, IKKE TRANSFORMASJON — godkjent av prosjektgruppen.
-- Koordinatene skrives ikke om, bare merkelappen. WKB-rundturen beholder hvert
-- tall bit for bit, så to geometrier som overlapper før kjøringen overlapper
-- like mye etterpå: LocationAreas, områdetilhørighet og alle avledede tabeller
-- er upåvirket og skal IKKE bygges om. Mot maskeringsrutene gir ommerkingen
-- inntil ~1 m tilsynelatende forskyvning (datumene har glidd fra hverandre
-- ~2,5 cm/år siden 1989) — uten betydning mot ruter på 4, 8 og 16 km.
--
-- RØRER IKKE Maskeringsruter_*. De er allerede 25833. Skriptet verifiserer det
-- og avbryter hvis de ikke er det.
--
-- AVBRYTER FRAMFOR Å GJETTE. Finnes det en geometri med en SRID som verken er
-- 32633 eller 25833, stopper skriptet uten å skrive noe.
--
-- Z OG M BEVARES. STAsBinary() mister Z- og M-verdier. Ingen geometri i den
-- produksjonslike basen har dem (Area 0 av 4 224, SensitiveObservationData
-- 0 av 606, Location 0 i et 1 %-utvalg), men skriptet stoler ikke på det: rader
-- med HasZ eller HasM går via AsTextZM(), som beholder begge.
--
-- KAN AVBRYTES OG STARTES PÅ NYTT. Location fører vannmerke per batch i
-- dbo.BackfillProgress, og hver setning har WHERE ... STSrid = 32633, så en ny
-- kjøring gjør bare det som gjenstår. Blir den romlige indeksen stående
-- deaktivert etter et avbrudd, bygger neste kjøring den opp igjen.
--
-- MÅ KJØRE UTEN OMSLUTTENDE TRANSAKSJON. 5,1 M rader i én transaksjon er en
-- loggfil ingen vil ha, og en timeout ville rullet hele kjøringen tilbake.
--
-- KJØRETID: titalls minutter i et produksjonslikt miljø. Gjenoppbyggingen av
-- den romlige indeksen på Location.Geometry er det tyngste enkeltsteget.
-- ============================================================================

SET NOCOUNT ON;

DECLARE @Fra INT = 32633;
DECLARE @Til INT = 25833;

DECLARE @Msg NVARCHAR(500);
DECLARE @Sql NVARCHAR(MAX);
DECLARE @BatchSize INT = 250000;
DECLARE @MinId INT, @MaxId INT, @CurrentId INT, @BatchEnd INT;
DECLARE @Rows INT, @Total BIGINT, @SectionStart DATETIME2;
DECLARE @MedGeometri BIGINT, @AntallFra BIGINT, @AntallTil BIGINT, @AntallAnnet BIGINT;
DECLARE @RunStart DATETIME2 = SYSUTCDATETIME();

-- ---------------------------------------------------------------------------
-- Preflight — skjemaet må være på plass før ommerkingen gir mening.
-- ---------------------------------------------------------------------------
IF COL_LENGTH('dbo.Location', 'Geometry') IS NULL
    OR COL_LENGTH('dbo.Area', 'WktPolygon') IS NULL
    OR COL_LENGTH('dbo.SensitiveObservationData', 'Geometry') IS NULL
BEGIN
    RAISERROR('AVBRUTT: Location.Geometry, Area.WktPolygon eller SensitiveObservationData.Geometry finnes ikke. Kjoer alle migrasjoner foerst.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- Fremdriftstabell. Samme tabell og samme form som BackfillAll bruker — én
-- mekanisme for vannmerker i denne basen, ikke to. Seksjonsnavnet her er
-- prefikset SRID_ og rører ingen av BackfillAlls rader.
IF OBJECT_ID('dbo.BackfillProgress') IS NULL
BEGIN
    CREATE TABLE dbo.BackfillProgress (
        Section         NVARCHAR(64)  NOT NULL PRIMARY KEY,
        LastCompletedId INT           NOT NULL,
        UpdatedAt       DATETIME2     NOT NULL CONSTRAINT DF_BackfillProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    RAISERROR('Opprettet dbo.BackfillProgress.', 0, 1) WITH NOWAIT;
END

-- ---------------------------------------------------------------------------
-- Maskeringsrutene skal allerede være 25833. Er de ikke det, er antakelsen hele
-- ommerkingen bygger på feil, og da skal ingenting skrives.
-- ---------------------------------------------------------------------------
DECLARE @RuteAvvik INT = 0;

IF OBJECT_ID('dbo.Maskeringsruter_4x4km') IS NOT NULL
    AND EXISTS (SELECT 1 FROM dbo.Maskeringsruter_4x4km WHERE SHAPE IS NOT NULL AND SHAPE.STSrid <> @Til)
    SET @RuteAvvik = 1;
IF OBJECT_ID('dbo.Maskeringsruter_8x8km') IS NOT NULL
    AND EXISTS (SELECT 1 FROM dbo.Maskeringsruter_8x8km WHERE SHAPE IS NOT NULL AND SHAPE.STSrid <> @Til)
    SET @RuteAvvik = 1;
IF OBJECT_ID('dbo.Maskeringsruter_16x16km') IS NOT NULL
    AND EXISTS (SELECT 1 FROM dbo.Maskeringsruter_16x16km WHERE SHAPE IS NOT NULL AND SHAPE.STSrid <> @Til)
    SET @RuteAvvik = 1;

IF @RuteAvvik = 1
BEGIN
    RAISERROR('AVBRUTT: Maskeringsruter_* har geometri som ikke er 25833. Forutsetningen for ommerkingen holder ikke - undersoek foer du kjoerer paa nytt.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- ---------------------------------------------------------------------------
-- Opptelling per kolonne. Én gjennomgang gir både fasittallene og svaret på om
-- det finnes SRID-er vi ikke kjenner. To spørringer ville kostet to skanninger
-- av 5,1 M geometrier.
-- ---------------------------------------------------------------------------
DECLARE @Fasit TABLE (
    Kolonne     NVARCHAR(64) NOT NULL PRIMARY KEY,
    MedGeometri BIGINT       NOT NULL,
    AntallFra   BIGINT       NOT NULL,
    AntallTil   BIGINT       NOT NULL,
    AntallAnnet BIGINT       NOT NULL
);

INSERT INTO @Fasit (Kolonne, MedGeometri, AntallFra, AntallTil, AntallAnnet)
SELECT 'Area.WktPolygon',
       COUNT(*),
       SUM(CASE WHEN WktPolygon.STSrid = @Fra THEN 1 ELSE 0 END),
       SUM(CASE WHEN WktPolygon.STSrid = @Til THEN 1 ELSE 0 END),
       SUM(CASE WHEN WktPolygon.STSrid NOT IN (@Fra, @Til) THEN 1 ELSE 0 END)
FROM dbo.Area WHERE WktPolygon IS NOT NULL;

INSERT INTO @Fasit (Kolonne, MedGeometri, AntallFra, AntallTil, AntallAnnet)
SELECT 'SensitiveObservationData.Geometry',
       COUNT(*),
       SUM(CASE WHEN Geometry.STSrid = @Fra THEN 1 ELSE 0 END),
       SUM(CASE WHEN Geometry.STSrid = @Til THEN 1 ELSE 0 END),
       SUM(CASE WHEN Geometry.STSrid NOT IN (@Fra, @Til) THEN 1 ELSE 0 END)
FROM dbo.SensitiveObservationData WHERE Geometry IS NOT NULL;

INSERT INTO @Fasit (Kolonne, MedGeometri, AntallFra, AntallTil, AntallAnnet)
SELECT 'Location.Geometry',
       COUNT(*),
       SUM(CASE WHEN Geometry.STSrid = @Fra THEN 1 ELSE 0 END),
       SUM(CASE WHEN Geometry.STSrid = @Til THEN 1 ELSE 0 END),
       SUM(CASE WHEN Geometry.STSrid NOT IN (@Fra, @Til) THEN 1 ELSE 0 END)
FROM dbo.Location WHERE Geometry IS NOT NULL;

DECLARE fasit CURSOR LOCAL FAST_FORWARD FOR
    SELECT Kolonne, MedGeometri, AntallFra, AntallTil, AntallAnnet FROM @Fasit ORDER BY Kolonne;
DECLARE @Kolonne NVARCHAR(64);
OPEN fasit;
FETCH NEXT FROM fasit INTO @Kolonne, @MedGeometri, @AntallFra, @AntallTil, @AntallAnnet;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Msg = CONCAT(@Kolonne, ' | med geometri: ', FORMAT(@MedGeometri, 'N0'),
                      ' | 32633: ', FORMAT(@AntallFra, 'N0'),
                      ' | 25833: ', FORMAT(@AntallTil, 'N0'),
                      ' | annet: ', FORMAT(@AntallAnnet, 'N0'));
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    FETCH NEXT FROM fasit INTO @Kolonne, @MedGeometri, @AntallFra, @AntallTil, @AntallAnnet;
END
CLOSE fasit;
DEALLOCATE fasit;

IF EXISTS (SELECT 1 FROM @Fasit WHERE AntallAnnet > 0)
BEGIN
    RAISERROR('AVBRUTT: fant geometri med en SRID som verken er 32633 eller 25833. Skriptet gjetter ikke - se opptellingen over.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- ---------------------------------------------------------------------------
-- Stikkprøve av WKT før ommerkingen. Kontrollen nederst sammenligner mot denne
-- og hever feil hvis ett eneste tall har endret seg. Det er ommerkingens
-- eneste løfte, så det skal være målt og ikke antatt.
--
-- AsTextZM() og ikke STAsText(): sistnevnte ville skjult en tapt Z-verdi.
-- ---------------------------------------------------------------------------
CREATE TABLE #WktFoer (
    Kolonne NVARCHAR(64)   NOT NULL,
    Id      INT            NOT NULL,
    Wkt     NVARCHAR(MAX)  NULL,
    PRIMARY KEY (Kolonne, Id)
);

INSERT INTO #WktFoer (Kolonne, Id, Wkt)
SELECT 'Area.WktPolygon', Id, WktPolygon.AsTextZM() FROM dbo.Area WHERE WktPolygon IS NOT NULL;

INSERT INTO #WktFoer (Kolonne, Id, Wkt)
SELECT 'SensitiveObservationData.Geometry', ObservationId, Geometry.AsTextZM()
FROM dbo.SensitiveObservationData WHERE Geometry IS NOT NULL;

-- Location har 5,1 M rader. 2 000 fra hver ende av klyngeindeksen holder som
-- stikkprøve og koster to seeks.
INSERT INTO #WktFoer (Kolonne, Id, Wkt)
SELECT 'Location.Geometry', f.Id, f.Wkt FROM (
    SELECT TOP (2000) Id, Geometry.AsTextZM() AS Wkt FROM dbo.Location WHERE Geometry IS NOT NULL ORDER BY Id
) f;

INSERT INTO #WktFoer (Kolonne, Id, Wkt)
SELECT 'Location.Geometry', b.Id, b.Wkt FROM (
    SELECT TOP (2000) Id, Geometry.AsTextZM() AS Wkt FROM dbo.Location WHERE Geometry IS NOT NULL ORDER BY Id DESC
) b
WHERE NOT EXISTS (SELECT 1 FROM #WktFoer w WHERE w.Kolonne = 'Location.Geometry' AND w.Id = b.Id);

SET @Msg = CONCAT('Stikkproeve av WKT lagret: ', (SELECT FORMAT(COUNT(*), 'N0') FROM #WktFoer), ' geometrier.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ===========================================================================
-- SEKSJON A: Area.WktPolygon (4 224 rader)
--
-- Area.Centroid er en beregnet kolonne ([WktPolygon].[STCentroid]()) og følger
-- automatisk. Den romlige indeksen SpatialIndex-WktPolygon står urørt — 4 224
-- rader er for få til at vedlikeholdet av den koster noe.
-- ===========================================================================
RAISERROR('=== SEKSJON A: Area.WktPolygon ===', 0, 1) WITH NOWAIT;

UPDATE dbo.Area
SET WktPolygon = CASE WHEN WktPolygon.HasZ = 1 OR WktPolygon.HasM = 1
                      THEN geometry::STGeomFromText(WktPolygon.AsTextZM(), @Til)
                      ELSE geometry::STGeomFromWKB(WktPolygon.STAsBinary(), @Til) END
WHERE WktPolygon IS NOT NULL AND WktPolygon.STSrid = @Fra;

SET @Msg = CONCAT('Seksjon A ferdig | ommerket: ', FORMAT(@@ROWCOUNT, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ===========================================================================
-- SEKSJON B: SensitiveObservationData.Geometry (606 av 2 196 rader)
--
-- Ingen romlig indeks på kolonnen.
-- ===========================================================================
RAISERROR('=== SEKSJON B: SensitiveObservationData.Geometry ===', 0, 1) WITH NOWAIT;

UPDATE dbo.SensitiveObservationData
SET Geometry = CASE WHEN Geometry.HasZ = 1 OR Geometry.HasM = 1
                    THEN geometry::STGeomFromText(Geometry.AsTextZM(), @Til)
                    ELSE geometry::STGeomFromWKB(Geometry.STAsBinary(), @Til) END
WHERE Geometry IS NOT NULL AND Geometry.STSrid = @Fra;

SET @Msg = CONCAT('Seksjon B ferdig | ommerket: ', FORMAT(@@ROWCOUNT, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ===========================================================================
-- SEKSJON C: Location.Geometry (5 100 794 rader)
--
-- Batchvis på klyngeindeksen med vannmerke per batch. Intervallet er bare en
-- løkkegrense; WHERE ... STSrid = 32633 er det som avgjør hva som skrives, så
-- en ny kjøring kan trygt gå over samme intervall.
-- ===========================================================================
RAISERROR('=== SEKSJON C: Location.Geometry ===', 0, 1) WITH NOWAIT;

SELECT @MinId = MIN(Id), @MaxId = MAX(Id) FROM dbo.Location;

-- Tom Location-tabell er en gyldig tilstand, ikke en feil: det er slik en fersk
-- lokal database ser ut. Vi returnerer ikke — kontrollene nederst skal kjøre.
IF @MinId IS NULL
BEGIN
    RAISERROR('Ingen rader i dbo.Location - hopper over seksjon C.', 0, 1) WITH NOWAIT;
    SET @MinId = 0;
    SET @MaxId = -1;
END

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'SRID_LocationGeometry'), @MinId - 1) + 1;

-- ---------------------------------------------------------------------------
-- Den romlige indeksen deaktiveres under skrivingen.
--
-- En masseoppdatering av 5,1 M geometrier med romlig indeks på kolonnen betyr
-- at indeksen vedlikeholdes per rad — cells_per_object indeksrader for hver
-- geometri. Deaktivert blir den ikke vedlikeholdt, og bygges opp igjen nederst.
--
-- TRY/CATCH fordi deaktivering av en romlig indeks er noe som enten går eller
-- ikke, avhengig av utgave og versjon. Går den ikke, er svaret å kjøre videre
-- med indeksen på plass — tregere, men korrekt — ikke å stoppe.
--
-- Gjøres bare når det faktisk er arbeid igjen. Ellers ville hver kjøring kostet
-- et fullt gjenoppbygg uten å skrive en eneste rad.
-- ---------------------------------------------------------------------------
DECLARE @SpatialIndex SYSNAME = (
    SELECT TOP 1 i.name FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('dbo.Location') AND i.type = 4);

IF @SpatialIndex IS NOT NULL AND @CurrentId <= @MaxId
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.Location') AND name = @SpatialIndex AND is_disabled = 0)
BEGIN
    BEGIN TRY
        SET @Sql = N'ALTER INDEX ' + QUOTENAME(@SpatialIndex) + N' ON dbo.Location DISABLE;';
        EXEC sp_executesql @Sql;
        SET @Msg = CONCAT('Deaktiverte romlig indeks ', @SpatialIndex, ' (bygges opp igjen til slutt).');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    END TRY
    BEGIN CATCH
        SET @Msg = CONCAT('Kunne ikke deaktivere ', @SpatialIndex, ' (', ERROR_MESSAGE(), '). Kjoerer videre med indeksen paa plass - tregere, men korrekt.');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    END CATCH
END

SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Seksjon C allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    -- OPTION (RECOMPILE): @CurrentId og @BatchEnd er lokale variabler, og uten
    -- den gjetter optimalisereren en fast andel av tabellen for intervallet.
    -- Predikatet ligger på klyngeindeksen, så planen blir en seek uansett, men
    -- estimatet styrer minnetildelingen.
    UPDATE dbo.Location
    SET Geometry = CASE WHEN Geometry.HasZ = 1 OR Geometry.HasM = 1
                        THEN geometry::STGeomFromText(Geometry.AsTextZM(), @Til)
                        ELSE geometry::STGeomFromWKB(Geometry.STAsBinary(), @Til) END
    WHERE Id >= @CurrentId AND Id <= @BatchEnd
      AND Geometry IS NOT NULL AND Geometry.STSrid = @Fra
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    -- Vannmerket settes etter batchen. Uten transaksjon rundt er batchen alt
    -- committet, så vannmerket er sant i det øyeblikket det skrives.
    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'SRID_LocationGeometry' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | C | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Ommerket: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

-- ---------------------------------------------------------------------------
-- Gjenoppbygging av den romlige indeksen. Betingelsen er at den står
-- deaktivert, ikke at denne kjøringen deaktiverte den — da reparerer kjøringen
-- også et tidligere avbrudd.
-- ---------------------------------------------------------------------------
IF @SpatialIndex IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.Location') AND name = @SpatialIndex AND is_disabled = 1)
BEGIN
    SET @Msg = CONCAT('Bygger opp romlig indeks ', @SpatialIndex, ' igjen (tyngste steget)...');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    SET @Sql = N'ALTER INDEX ' + QUOTENAME(@SpatialIndex) + N' ON dbo.Location REBUILD;';
    EXEC sp_executesql @Sql;
    RAISERROR('Romlig indeks gjenoppbygget.', 0, 1) WITH NOWAIT;
END


-- ===========================================================================
-- VERIFISERING. Kjøringen kan ikke bli stående som vellykket med en base som
-- er halvveis ommerket.
-- ===========================================================================
RAISERROR('=== VERIFISERING ===', 0, 1) WITH NOWAIT;

-- 1. Én SRID per kolonne, og den skal være 25833.
IF EXISTS (SELECT 1 FROM dbo.Area WHERE WktPolygon IS NOT NULL AND WktPolygon.STSrid <> @Til)
    OR EXISTS (SELECT 1 FROM dbo.SensitiveObservationData WHERE Geometry IS NOT NULL AND Geometry.STSrid <> @Til)
    OR EXISTS (SELECT 1 FROM dbo.Location WHERE Geometry IS NOT NULL AND Geometry.STSrid <> @Til)
BEGIN
    RAISERROR('AVBRUTT: det finnes fortsatt geometri som ikke er 25833. Kjoer skriptet paa nytt - det fortsetter der det slapp.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- 2. Radtall per kolonne uendret. En tapt geometri ville blitt NULL og ellers
--    gått upåaktet hen.
IF EXISTS (
    SELECT 1 FROM @Fasit f
    JOIN (
        SELECT 'Area.WktPolygon' AS Kolonne, COUNT(*) AS Naa FROM dbo.Area WHERE WktPolygon IS NOT NULL
        UNION ALL
        SELECT 'SensitiveObservationData.Geometry', COUNT(*) FROM dbo.SensitiveObservationData WHERE Geometry IS NOT NULL
        UNION ALL
        SELECT 'Location.Geometry', COUNT(*) FROM dbo.Location WHERE Geometry IS NOT NULL
    ) n ON n.Kolonne = f.Kolonne
    WHERE n.Naa <> f.MedGeometri)
BEGIN
    RAISERROR('AVBRUTT: antall rader med geometri har endret seg under kjoeringen. Noe har mistet geometrien sin.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- 3. Koordinatene står urørt. Dette er hele poenget med en WKB-rundtur framfor
--    en transformasjon.
DECLARE @WktAvvik INT = (
    SELECT COUNT(*) FROM #WktFoer w
    LEFT JOIN dbo.Area a ON w.Kolonne = 'Area.WktPolygon' AND a.Id = w.Id
    LEFT JOIN dbo.SensitiveObservationData s ON w.Kolonne = 'SensitiveObservationData.Geometry' AND s.ObservationId = w.Id
    LEFT JOIN dbo.Location l ON w.Kolonne = 'Location.Geometry' AND l.Id = w.Id
    WHERE w.Wkt <> CASE w.Kolonne
                       WHEN 'Area.WktPolygon' THEN a.WktPolygon.AsTextZM()
                       WHEN 'SensitiveObservationData.Geometry' THEN s.Geometry.AsTextZM()
                       WHEN 'Location.Geometry' THEN l.Geometry.AsTextZM()
                   END);

IF @WktAvvik > 0
BEGIN
    SET @Msg = CONCAT('AVBRUTT: ', @WktAvvik, ' geometrier i stikkproeven har endret WKT. Ommerkingen skal ikke flytte en eneste koordinat.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
    RETURN;
END

-- 4. Den romlige indeksen står ikke igjen deaktivert.
IF @SpatialIndex IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.Location') AND name = @SpatialIndex AND is_disabled = 1)
BEGIN
    RAISERROR('AVBRUTT: den romlige indeksen paa Location.Geometry staar deaktivert. Kjoer skriptet paa nytt.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- 5. Det ommerkingen var til for: en romlig sammenligning mellom en
--    observasjonsgeometri og en maskeringsrute gir nå et svar, ikke NULL.
IF OBJECT_ID('dbo.Maskeringsruter_4x4km') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Location WHERE Geometry IS NOT NULL)
   AND EXISTS (SELECT 1 FROM dbo.Maskeringsruter_4x4km WHERE SHAPE IS NOT NULL)
BEGIN
    DECLARE @Treff INT = (
        SELECT l.Geometry.STIntersects(r.SHAPE)
        FROM (SELECT TOP 1 Geometry FROM dbo.Location WHERE Geometry IS NOT NULL ORDER BY Id) l
        CROSS JOIN (SELECT TOP 1 SHAPE FROM dbo.Maskeringsruter_4x4km WHERE SHAPE IS NOT NULL ORDER BY OBJECTID) r);

    IF @Treff IS NULL
    BEGIN
        RAISERROR('AVBRUTT: STIntersects mellom Location.Geometry og Maskeringsruter_4x4km gir fortsatt NULL. Merkingen er ikke samlet.', 16, 1) WITH NOWAIT;
        RETURN;
    END
    RAISERROR('STIntersects mot maskeringsrutene gir et svar (ikke NULL).', 0, 1) WITH NOWAIT;
END

DROP TABLE #WktFoer;

SET @Msg = CONCAT('FERDIG. Alle geometrier er merket 25833. Kjoeretid: ',
                  DATEDIFF(SECOND, @RunStart, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

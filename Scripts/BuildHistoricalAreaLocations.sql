-- ============================================================================
-- BuildHistoricalAreaLocations — kobler lokasjoner til historiske fylker,
-- kommuner og havområder (Area.IsCurrent = 0) i HistoricalAreaLocation.
--
-- HVORFOR
-- Filteret på historiske områder trenger lokasjonene i hvert område.
-- LocationAreas har bare koblinger for gjeldende områder, og
-- ObservationEntityIndex er utledet av den. Koblingene ligger derfor i en egen
-- tabell som bare filteret på historiske områder leser.
--
-- Søket i API-et viser bare historiske områder som har rader her. Er skriptet
-- ikke kjørt, finnes de altså ikke i søket.
--
-- REGEL: samme som LocationAreas — STIntersects mellom Location.Geometry og
-- Area.WktPolygon, uten buffer. Se RebuildLocationAreasForAreaTypes.sql for
-- verifiseringen av at det er regelen oppstrømsprosessen bruker.
--
-- TIDSBRUK, målt lokalt under Rosetta: alle 443 historiske områder tok 36
-- minutter og ga 12,6 millioner rader. Tregest var Akershus (4,3 min) og Viken
-- (53 s). Skriptet kan avbrytes og kjøres på nytt: hvert område settes inn med
-- én setning, og områder som allerede har rader hoppes over.
--
-- NYE LOKASJONER kobles ikke av seg selv. Kjør med @ByggAlle = 1 for å bygge
-- alt på nytt, til importen tar over vedlikeholdet (se ImportJob).
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ByggAlle BIT = 0;   -- 1 = slett og bygg alle områdene på nytt

DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @Msg NVARCHAR(400);

RAISERROR('=== BuildHistoricalAreaLocations ===', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('ByggAlle=', @ByggAlle);
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- Områdetypene filteret tilbyr: kommune, fylke, havområde og Svalbard/Jan Mayen.
DROP TABLE IF EXISTS #Typer;
CREATE TABLE #Typer (AreaTypeId INT PRIMARY KEY);
INSERT INTO #Typer (AreaTypeId) VALUES (1), (2), (4), (6);

-- ---------------------------------------------------------------------------
-- Forkontroll: SRID
--
-- Ulik SRID gir NULL fra STIntersects, ikke feil, og et område uten treff ser
-- da ut som et gyldig svar.
-- ---------------------------------------------------------------------------
DECLARE @LocSrid INT = (SELECT TOP 1 Geometry.STSrid FROM dbo.Location WHERE Geometry IS NOT NULL);

IF EXISTS (SELECT 1 FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
           WHERE a.IsCurrent = 0 AND a.WktPolygon IS NOT NULL AND a.WktPolygon.STSrid <> @LocSrid)
BEGIN
    SELECT a.Id, a.AreaTypeId, a.Fid, a.Name, a.WktPolygon.STSrid AS Srid
    FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
    WHERE a.IsCurrent = 0 AND a.WktPolygon IS NOT NULL AND a.WktPolygon.STSrid <> @LocSrid;
    SET @Msg = CONCAT('AVBRUTT: omraadene over har annen SRID enn Location.Geometry (', @LocSrid, ').');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
    RETURN;
END

-- ---------------------------------------------------------------------------
-- Steg 1: fjern rader for områder som ikke lenger er historiske
--
-- Et område som er slettet eller satt tilbake til gjeldende hører ikke hjemme
-- her: et gjeldende område filtreres via ObservationEntityIndex.
-- ---------------------------------------------------------------------------
DELETE h
FROM dbo.HistoricalAreaLocation h
WHERE NOT EXISTS (SELECT 1 FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
                  WHERE a.Id = h.AreaId AND a.IsCurrent = 0 AND a.IsDeleted = 0);

SET @Msg = CONCAT('Steg 1: slettet ', FORMAT(@@ROWCOUNT, 'N0'), ' rader for omraader som ikke lenger er historiske.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Steg 2: bygg ett område av gangen
--
-- Geometrien i en variabel: det er slik den romlige indeksen på
-- Location.Geometry faktisk brukes. Ugyldige polygoner repareres først, siden
-- SQL Server avviser dem i STIntersects.
-- ---------------------------------------------------------------------------
DECLARE @Res TABLE (AreaId INT, AreaTypeId INT, Fid NVARCHAR(50), Navn NVARCHAR(200), SattInn BIGINT, Ms INT);

DECLARE @Id INT, @Type INT, @Fid NVARCHAR(50), @Navn NVARCHAR(200);
DECLARE @g geometry, @SattInn BIGINT, @t DATETIME2;

DECLARE omr CURSOR LOCAL FAST_FORWARD FOR
    SELECT a.Id, a.AreaTypeId, a.Fid, a.Name
    FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
    WHERE a.IsCurrent = 0 AND a.IsDeleted = 0
      AND (@ByggAlle = 1 OR NOT EXISTS (SELECT 1 FROM dbo.HistoricalAreaLocation h WHERE h.AreaId = a.Id))
    ORDER BY a.AreaTypeId, a.Id;

OPEN omr;
FETCH NEXT FROM omr INTO @Id, @Type, @Fid, @Navn;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @g = (SELECT CASE WHEN WktPolygon.STIsValid() = 1 THEN WktPolygon ELSE WktPolygon.MakeValid() END
              FROM dbo.Area WHERE Id = @Id);
    SET @t = SYSUTCDATETIME();

    IF @g IS NULL
    BEGIN
        SET @Msg = CONCAT('  AreaId ', @Id, ' (', @Navn, ') har ingen WktPolygon - hoppet over.');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        FETCH NEXT FROM omr INTO @Id, @Type, @Fid, @Navn;
        CONTINUE;
    END

    BEGIN TRANSACTION;

    DELETE FROM dbo.HistoricalAreaLocation WHERE AreaId = @Id;

    INSERT INTO dbo.HistoricalAreaLocation (AreaId, LocationId)
    SELECT @Id, l.Id
    FROM dbo.Location l
    WHERE l.Geometry.STIntersects(@g) = 1;

    SET @SattInn = @@ROWCOUNT;

    COMMIT TRANSACTION;

    INSERT INTO @Res VALUES (@Id, @Type, @Fid, @Navn, @SattInn, DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()));

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | type ', @Type,
                      ' | ', @Fid, ' ', LEFT(@Navn, 38),
                      ' | satt inn ', FORMAT(@SattInn, 'N0'),
                      ' | ', DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()), ' ms');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    FETCH NEXT FROM omr INTO @Id, @Type, @Fid, @Navn;
END

CLOSE omr;
DEALLOCATE omr;

-- ---------------------------------------------------------------------------
-- Rapport
-- ---------------------------------------------------------------------------
SELECT AreaTypeId, AreaId, Fid, Navn, SattInn, Ms FROM @Res ORDER BY AreaTypeId, AreaId;

SELECT COUNT(*) AS OmraaderBygget, SUM(SattInn) AS RaderSattInn,
       DATEDIFF(SECOND, @Start, SYSUTCDATETIME()) AS SekTotalt
FROM @Res;

-- Historiske områder uten rader vises ikke i søket.
SELECT a.Id, a.AreaTypeId, a.Fid, a.Name
FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
WHERE a.IsCurrent = 0 AND a.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.HistoricalAreaLocation h WHERE h.AreaId = a.Id);

DROP TABLE IF EXISTS #Typer;

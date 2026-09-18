-- ============================================================================
-- RebuildLocationAreasForAreaTypes — beregner LocationAreas på nytt for valgte
-- områdetyper ved romlig snitt mellom Location.Geometry og Area.WktPolygon.
--
-- HVORFOR
-- LocationAreas er kilden ObservationEntityIndex utledes fra
-- (Observation -> Location -> LocationAreas -> Area). Ingenting i dette repoet
-- fyller den; den kommer fra en prosess utenfor løsningen. Da Area ble oppdatert
-- i produksjon uten at LocationAreas fulgte med, kunne backfillen ikke utlede
-- noen rader for de nye områdene — og siden den også sletter rader som ikke lar
-- seg utlede, ble indeksen tømt for de typene i stedet for fylt.
--
-- Å reparere ObservationEntityIndex uten å reparere LocationAreas holder ikke:
-- neste backfill sletter radene igjen, fordi kilden fortsatt er tom.
--
-- METODEN ER VERIFISERT
-- Mot Artskart3IndexProdLikeTestMigrations, der LocationAreas er korrekt,
-- reproduserer STIntersects radantallet eksakt for alle ti områdene av type
-- 4 og 6 — 256 054 mot 256 054, null avvik, ett sekund totalt:
--
--   AreaId 450  Nordsjøen og Skagerak                64 071   168 ms
--   AreaId 451  Svalbard og Bjørnøya med havområder  75 600    60 ms
--   AreaId 452  Jan Mayen og fiskerisonen             4 339    16 ms
--   AreaId 453  Norges økonomiske sone m.m.          52 296   252 ms
--   AreaId 39312 Jan Mayen med kystnære områder       2 175    11 ms
--   AreaId 39313 Bjørnøya med kystnære områder        2 065    19 ms
--   AreaId 39314 Hopen med kystnære områder             206     8 ms
--   AreaId 39315 Svalbard med kystnære områder       54 960    36 ms
--   AreaId 39316 Kong Karls Land med kystnære           174     9 ms
--   AreaId 39317 Kvitøya med kystnære områder           168     5 ms
--
-- Det er sterk indikasjon på at oppstrømsprosessen bruker rent romlig snitt,
-- uten buffer eller senterpunktregel. Kjør likevel verifiseringen i et miljø der
-- LocationAreas er korrekt før du stoler på den for en ny områdetype.
--
-- FORUTSETNINGER — begge verifisert i prodlike:
--   Location.Geometry   SRID 32633, 5 100 794 rader, ingen NULL
--   Area.WktPolygon     SRID 32633 for alle områdetyper
--   Romlige indekser    SpatialIndex-Geometry og SpatialIndex-WktPolygon
--
-- Ulike SRID-er ville gitt NULL fra STIntersects, ikke feilmelding — altså null
-- treff og en tom tabell som ser ut som et gyldig resultat. Derfor sjekkes de
-- eksplisitt før noe kjøres.
--
-- LOOP OVER OMRÅDER, IKKE ÉN SET-BASERT JOIN. Med geometrien i en variabel
-- bruker hver spørring den romlige indeksen. Skrevet som join mot
-- a.WktPolygon er det ikke gitt at optimalisereren finner samme plan.
--
-- ============================================================================
-- KJØR FØRST MED @DryRun = 1.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun         BIT = 1;   -- 1 = rapporter bare, ikke skriv
DECLARE @UtfoerSletting BIT = 0;   -- se kommentaren ved steg 3

-- Områdetypene som bygges. Legg til 5 her hvis havområdene av den typen
-- også mangler.
DROP TABLE IF EXISTS #Typer;
CREATE TABLE #Typer (AreaTypeId INT PRIMARY KEY);
INSERT INTO #Typer (AreaTypeId) VALUES (4), (6);

DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @Msg NVARCHAR(400);

RAISERROR('=== RebuildLocationAreasForAreaTypes ===', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('DryRun=', @DryRun, ' Sletting=', @UtfoerSletting);
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Forkontroll 1: SRID
--
-- Ulik SRID gir NULL fra STIntersects, ikke feil. Resultatet blir null treff,
-- og en tom LocationAreas ser da ut som et gyldig svar. Stopp heller her.
-- ---------------------------------------------------------------------------
DECLARE @LocSrid INT, @AreaSrids INT;

SELECT TOP 1 @LocSrid = Geometry.STSrid FROM dbo.Location WHERE Geometry IS NOT NULL;

SELECT @AreaSrids = COUNT(DISTINCT a.WktPolygon.STSrid)
FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
WHERE a.IsCurrent = 1 AND a.WktPolygon IS NOT NULL;

IF @AreaSrids <> 1
BEGIN
    RAISERROR('AVBRUTT: omraadene har flere ulike SRID-er.', 0, 1) WITH NOWAIT;
    SELECT DISTINCT a.AreaTypeId, a.WktPolygon.STSrid AS Srid
    FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
    WHERE a.IsCurrent = 1 AND a.WktPolygon IS NOT NULL;
    RAISERROR('Alle maa ha samme SRID som Location.Geometry.', 16, 1) WITH NOWAIT;
    RETURN;
END

DECLARE @AreaSrid INT = (SELECT TOP 1 a.WktPolygon.STSrid FROM dbo.Area a
                         JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
                         WHERE a.IsCurrent = 1 AND a.WktPolygon IS NOT NULL);

IF @LocSrid <> @AreaSrid
BEGIN
    SET @Msg = CONCAT('AVBRUTT: Location.Geometry har SRID ', @LocSrid,
                      ', omraadene har ', @AreaSrid,
                      '. STIntersects ville returnert NULL for alle rader.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
    RETURN;
END

SET @Msg = CONCAT('SRID OK: begge er ', @LocSrid, '.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Forkontroll 2: omraader uten geometri
--
-- Et omraade uten polygon gir null treff. Uten denne sjekken ville det sett ut
-- som et omraade uten lokaliteter, ikke som manglende data.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
           WHERE a.IsCurrent = 1 AND a.WktPolygon IS NULL)
BEGIN
    RAISERROR('AVBRUTT: gjeldende omraader uten WktPolygon:', 0, 1) WITH NOWAIT;
    SELECT a.Id, a.AreaTypeId, a.Fid, a.Name
    FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
    WHERE a.IsCurrent = 1 AND a.WktPolygon IS NULL;
    RAISERROR('Geometrien maa paa plass foerst.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- ---------------------------------------------------------------------------
-- Steg 1-2: beregn og sett inn, ett omraade av gangen
-- ---------------------------------------------------------------------------
DECLARE @Res TABLE (AreaId INT, AreaTypeId INT, Fid NVARCHAR(50), Navn NVARCHAR(200),
                    Foer BIGINT, Beregnet BIGINT, SattInn BIGINT, Ms INT);

DECLARE @Id INT, @Type INT, @Fid NVARCHAR(50), @Navn NVARCHAR(200);
DECLARE @g geometry, @Beregnet BIGINT, @Foer BIGINT, @SattInn BIGINT, @t DATETIME2;

DECLARE omr CURSOR LOCAL FAST_FORWARD FOR
    SELECT a.Id, a.AreaTypeId, a.Fid, a.Name
    FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
    WHERE a.IsCurrent = 1
    ORDER BY a.AreaTypeId, a.Id;

OPEN omr;
FETCH NEXT FROM omr INTO @Id, @Type, @Fid, @Navn;

WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @Foer = COUNT_BIG(*) FROM dbo.LocationAreas WHERE AreaId = @Id;

    SET @g = (SELECT WktPolygon FROM dbo.Area WHERE Id = @Id);
    SET @t = SYSUTCDATETIME();
    SET @SattInn = 0;

    -- Geometrien i en variabel: det er slik den romlige indeksen faktisk brukes.
    SELECT @Beregnet = COUNT_BIG(*)
    FROM dbo.Location WITH (NOLOCK)
    WHERE Geometry.STIntersects(@g) = 1;

    IF @DryRun = 0
    BEGIN
        -- NOT EXISTS gjoer steget idempotent. PK er (LocationId, AreaId), saa en
        -- ny kjoering kan ikke lage duplikater uansett - men uten predikatet
        -- ville den feilet i stedet for aa hoppe over.
        INSERT INTO dbo.LocationAreas (LocationId, AreaId)
        SELECT l.Id, @Id
        FROM dbo.Location l
        WHERE l.Geometry.STIntersects(@g) = 1
          AND NOT EXISTS (SELECT 1 FROM dbo.LocationAreas la
                          WHERE la.LocationId = l.Id AND la.AreaId = @Id);

        SET @SattInn = @@ROWCOUNT;
    END

    INSERT INTO @Res VALUES (@Id, @Type, @Fid, @Navn, @Foer, @Beregnet, @SattInn,
                             DATEDIFF(MILLISECOND, @t, SYSUTCDATETIME()));

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | type ', @Type,
                      ' | ', LEFT(@Navn, 38),
                      ' | foer ', FORMAT(@Foer, 'N0'),
                      ' | beregnet ', FORMAT(@Beregnet, 'N0'),
                      ' | satt inn ', FORMAT(@SattInn, 'N0'));
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    FETCH NEXT FROM omr INTO @Id, @Type, @Fid, @Navn;
END

CLOSE omr;
DEALLOCATE omr;

-- ---------------------------------------------------------------------------
-- Steg 3: fjern koblinger som ikke lenger stemmer
--
-- AV SOM STANDARD, MED VILJE. Sletting er det som gjorde skade sist: et steg som
-- fjernet rader kjoerte foer noe hadde bekreftet at kilden kunne produsere
-- erstatninger. Her er rekkefoelgen snudd - innsettingen er alt gjort og
-- rapportert naar dette eventuelt kjoerer - men flagget krever fortsatt et
-- bevisst valg.
--
-- Trengs bare hvis et omraade har fatt ENDRET geometri. Er omraadene bare nye,
-- la det staa av.
-- ---------------------------------------------------------------------------
IF @UtfoerSletting = 1 AND @DryRun = 0
BEGIN
    RAISERROR('Sletter koblinger som ikke lenger snitter...', 0, 1) WITH NOWAIT;

    DECLARE @Slettet BIGINT = 0, @SlettetTotalt BIGINT = 0;

    DECLARE omr2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT a.Id FROM dbo.Area a JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
        WHERE a.IsCurrent = 1 ORDER BY a.Id;
    OPEN omr2; FETCH NEXT FROM omr2 INTO @Id;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @g = (SELECT WktPolygon FROM dbo.Area WHERE Id = @Id);

        DELETE la
        FROM dbo.LocationAreas la
        JOIN dbo.Location l ON l.Id = la.LocationId
        WHERE la.AreaId = @Id
          AND l.Geometry.STIntersects(@g) = 0;

        SET @Slettet = @@ROWCOUNT;
        SET @SlettetTotalt = @SlettetTotalt + @Slettet;

        IF @Slettet > 0
        BEGIN
            SET @Msg = CONCAT('  AreaId ', @Id, ': slettet ', FORMAT(@Slettet, 'N0'));
            RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        END

        FETCH NEXT FROM omr2 INTO @Id;
    END
    CLOSE omr2; DEALLOCATE omr2;

    SET @Msg = CONCAT('Slettet totalt: ', FORMAT(@SlettetTotalt, 'N0'));
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Steg 3 (sletting) hoppet over.', 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Rapport
-- ---------------------------------------------------------------------------
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('--- Per omraade ---', 0, 1) WITH NOWAIT;

SELECT AreaTypeId, AreaId, Fid, Navn, Foer, Beregnet, SattInn,
       Foer + SattInn AS Etter,
       CASE WHEN Foer + SattInn = Beregnet THEN 'OK' ELSE 'AVVIK' END AS Status,
       Ms
FROM @Res ORDER BY AreaTypeId, AreaId;

SELECT SUM(Foer) AS FoerTotalt, SUM(Beregnet) AS BeregnetTotalt,
       SUM(SattInn) AS SattInnTotalt,
       SUM(CASE WHEN Foer + SattInn <> Beregnet THEN 1 ELSE 0 END) AS AntallAvvik,
       DATEDIFF(SECOND, @Start, SYSUTCDATETIME()) AS SekTotalt
FROM @Res;

IF @DryRun = 1
    RAISERROR('DRY RUN - ingenting ble skrevet. Sett @DryRun = 0 for aa kjoere.', 0, 1) WITH NOWAIT;
ELSE
BEGIN
    RAISERROR('', 0, 1) WITH NOWAIT;
    RAISERROR('NESTE STEG: bygg ObservationEntityIndex med', 0, 1) WITH NOWAIT;
    RAISERROR('  Scripts/BackfillOceanAndSvalbardAreas.sql med @UtfoerSletting = 0', 0, 1) WITH NOWAIT;
END

DROP TABLE IF EXISTS #Typer;

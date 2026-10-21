/*
    Bygger områdebufferen (AreaCountCacheLevel1 / -Level2 / -BucketMember / -State).

    KJØRES AV MIGRASJONEN BuildCountCaches, og kan kjøres manuelt med samme virkning.

    Den tømmer og bygger på nytt: ingen blue/green, ingen resumering, ingen
    verifisering. Det er forsvarlig for et FØRSTEGANGSBYGG, der det ikke finnes en
    buffer å miste — Status settes til «Building» før tømmingen, så et avbrudd
    etterlater en ikke-klar buffer og oppslaget aggregerer som før.

    Det er IKKE forsvarlig som gjenoppbygging under trafikk: bufferen er borte i
    minuttene bygget tar, og alle kall faller tilbake til aggregering. Den jobben
    hører til importen, og den trenger blue/green.

    BØTTEDEFINISJONENE MÅ STEMME MED AreaCountCacheDimensions.
    Dimensjons-id-ene, NULL-bøtta og pargruppe-rekkefølgen er duplisert her. Endres
    registeret i C#, må dette skriptet endres i takt — og SchemaVersion under må
    oppdateres, ellers nekter oppslaget å bruke bufferen (som er poenget med den).

    PARGRUPPE-ID
    Tildeles som i C#: id-ene sortert stigende, deretter alle par (i, j) med i < j i
    den rekkefølgen, nummerert fra 1. ROW_NUMBER OVER (ORDER BY a.Id, b.Id) gir
    nøyaktig samme sekvens.

    KJØRING
    Sett @BuildLevel1 / @BuildLevel2 for å bygge ett nivå om gangen. Nivå 2 er 66
    grupperinger over 134 millioner rader og ~36 millioner rader å sette inn.

    Bygges bare ETT nivå, blir Status stående på «Building» og bufferen brukes
    ikke — det andre nivået er da fra et eldre datasett. Begge flaggene må være
    satt i samme kjøring for at bufferen skal stemples klar.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Temptabellene ryddes først.
--
-- Migrasjonen BuildCountCaches kjører dette skriptet og BuildLocationCountCache.sql
-- på SAMME tilkobling, og en #temp-tabell lever til sesjonen lukkes — ikke til
-- batchen slutter. De to skriptene deler sju navn, så uten denne ryddingen feiler
-- det andre på «There is already an object named '#dim'». Gjelder også manuell
-- kjøring: to kjøringer i samme SSMS-fane traff samme feil.
DROP TABLE IF EXISTS #dim, #par, #vern, #vernsig, #vernbucket, #proj, #projsig, #projbucket;

DECLARE @BuildLevel1 BIT = 1;
DECLARE @BuildLevel2 BIT = 1;

-- Hentet fra AreaCountCacheDimensions.SchemaVersion. Stemmer ikke denne med koden,
-- ignoreres bufferen — med vilje.
DECLARE @SchemaVersion INT = 306158581;

DECLARE @NullBucket INT = -2147483648;   -- int.MinValue
DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @T DATETIME2, @Msg NVARCHAR(400), @Rows BIGINT;

-- ---------------------------------------------------------------------------
-- Dimensjonsregisteret, speilet fra C#
--
-- Uttrykket er hva raden skal bøttes på.
--
-- NULL-BØTTA BYGGES IKKE
-- Kolonner som kan være NULL foldes til sentinelen -2147483648, og rader med den
-- verdien filtreres BORT før innsetting. De kan aldri velges: et filter peker alltid
-- ut konkrete verdier, og intervalldimensjonene utelukker sentinelen eksplisitt i
-- AreaCountCacheService. Å bygge dem var ren dødvekt — målt til 8,8 % av toernivået
-- (3 413 281 rader, ~85 MB) og 1,0 % av ettnivået.
--
-- Sentinelen står likevel i uttrykkene, som vakt mot at en NULL skal nå en NOT NULL-
-- kolonne og velte hele bygget.
--
-- MERK: skulle filteret en dag få et «uten verdi»-valg — «vis observasjoner uten
-- registrert atferd» — må dette gjøres om. Da blir NULL-bøtta meningsfull.
-- ---------------------------------------------------------------------------
CREATE TABLE #dim (Id TINYINT PRIMARY KEY, Navn VARCHAR(40), Uttrykk NVARCHAR(200));
INSERT #dim VALUES
 (2,  'Regstatus',      N'CAST(i.RegistrationStatusId AS INT)'),
 (3,  'Atferd',         N'ISNULL(CAST(i.BehaviorId AS INT), -2147483648)'),
 (4,  'Funntype',       N'i.BasisOfRecordId'),
 (5,  'Kategori',       N'ISNULL(i.CategoryId, -2147483648)'),
 (6,  'Institusjon',    N'ISNULL(i.InstitutionOrgId, -2147483648)'),
 (7,  'Taksongruppe',   N'i.TaxonGroupId'),
 (8,  'Takson',         N'ISNULL(i.OrderTaxonId, -2147483648)'),
 (9,  'Datasett',       N'ISNULL(i.DatasetOrgId, -2147483648)'),
 (10, 'Periode',        N'ISNULL(YEAR(i.DateTimeCollected) * 100 + MONTH(i.DateTimeCollected), -2147483648)'),
 (11, 'Koordpresisjon', N'ISNULL(i.CoordinatePrecisionInMeters, -2147483648)'),
 -- Maalt 0 observasjoner uten geografisk omraade: hver observasjon ligger i minst ett.
 -- NULL-boetta er dermed tom, i motsetning til den gamle verneomraade-dimensjonen der
 -- 96 551 482 indeksrader havnet der.
 --
 -- ISNULL staar likevel som vakt. Bucket-kolonnene er NOT NULL, saa en rad uten
 -- geografi ville faatt INSERT-en til aa feile med "Cannot insert the value NULL".
 -- Det er en hoeylytt feil, ikke et stille tap - men den ville stoppet hele bygget
 -- midt i, og sentinelen er billigere enn aa maatte finne ut hvorfor.
 (12, 'Geografi',       N'ISNULL(v.BucketId, -2147483648)'),
 (13, 'Prosjekt',       N'ISNULL(p.BucketId, -2147483648)');

-- Joinene som trengs når en dimensjon er flerverdi. Settes inn i spørringen bare
-- når dimensjonen faktisk er med, så de 55 parene uten dem slipper kostnaden.
DECLARE @JoinVern NVARCHAR(200) = N' LEFT JOIN #vern v ON v.ObservationId = i.ObservationId';
DECLARE @JoinProj NVARCHAR(200) = N' LEFT JOIN #proj p ON p.ObservationId = i.ObservationId';

-- ---------------------------------------------------------------------------
-- MonthCollected MÅ stemme med MONTH(DateTimeCollected)
--
-- Periodedimensjonen bøttes på YEAR * 100 + MONTH(i.DateTimeCollected), mens
-- tellingen filtrerer på i.MonthCollected (SearchRepository, månedsgrenen). To
-- uttrykk som skal gi samme tall — og kontrakten er at bufferen skal gi NØYAKTIG
-- det tellingen gir, ellers er den feil.
--
-- Kolonnen er ingen beregnet kolonne her: på Observation er MonthCollected
-- beregnet av databasen og kan ikke sprike, men på ObservationEntityIndex er den
-- en vanlig TINYINT fylt av en backfill (se migrasjon 20260920125459). En kopi
-- kan bli stående igjen når kilden endres.
--
-- ALLE TRE TILFELLENE ER AVVIK, OG DE SLÅR UT HVER SIN VEI:
--   måned <> MONTH(dato)   bufferen teller raden i feil månedsbøtte
--   måned NULL, dato satt  bufferen legger den i NULL-bøtta, som utelukkes, mens
--                          et filter på bare årstall teller den med
--   dato NULL, måned satt  bufferen legger den i NULL-bøtta, mens et
--                          månedsfilter teller den med
--
-- Derfor sjekkes invarianten her, framfor å bytte uttrykket til i.MonthCollected:
-- det ville flyttet avviket til rad to og tre i tabellen over, ikke fjernet det.
-- Holder invarianten, gir de to uttrykkene samme tall, og hvilket av dem skriptet
-- bruker er likegyldig.
--
-- Står FØR Status settes til Building: en buffer som finnes og er i orden skal
-- ikke slås av fordi en forutsetning ikke holder.
-- ---------------------------------------------------------------------------
DECLARE @MaanedAvvik BIGINT, @MaanedMangler BIGINT, @MaanedUtenDato BIGINT;

SELECT @MaanedAvvik = COUNT_BIG(CASE WHEN i.DateTimeCollected IS NOT NULL
                                      AND i.MonthCollected IS NOT NULL
                                      AND i.MonthCollected <> MONTH(i.DateTimeCollected)
                                     THEN 1 END),
       @MaanedMangler = COUNT_BIG(CASE WHEN i.DateTimeCollected IS NOT NULL
                                        AND i.MonthCollected IS NULL
                                       THEN 1 END),
       @MaanedUtenDato = COUNT_BIG(CASE WHEN i.DateTimeCollected IS NULL
                                         AND i.MonthCollected IS NOT NULL
                                        THEN 1 END)
FROM dbo.ObservationEntityIndex i;

IF @MaanedAvvik + @MaanedMangler + @MaanedUtenDato > 0
BEGIN
    SET @Msg = CONCAT('MonthCollected stemmer ikke med DateTimeCollected: ',
                      FORMAT(@MaanedAvvik, 'N0'), ' avviker, ',
                      FORMAT(@MaanedMangler, 'N0'), ' mangler maaned med dato satt, ',
                      FORMAT(@MaanedUtenDato, 'N0'), ' har maaned uten dato. ',
                      'Periodebufferen ville gitt andre tall enn tellingen. Bufferen ',
                      'bygges IKKE - kjoer migrasjon 20260920125459 om igjen eller ',
                      'reparer kolonnen, og proev paa nytt.');
    RAISERROR(@Msg, 16, 1);
    RETURN;
END

-- Status settes til Building FØR tømmingen, ikke etter. Rekkefølgen er hele vernet:
-- hver setning er sin egen transaksjon (XACT_ABORT uten omsluttende transaksjon), så
-- feiler tømmingen halvveis, er bufferen allerede merket ikke-klar. Settes statusen
-- etterpå, etterlater et avbrudd en TOM buffer som fortsatt leses som Ready — blankt
-- kart, status 200, ingen feilmelding. Oppslaget krever Ready, så en avbrutt kjøring
-- skal etterlate en buffer som ikke brukes, framfor en halv buffer som brukes.
MERGE dbo.AreaCountCacheState AS t
USING (SELECT CAST(1 AS TINYINT) AS Id) AS s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET Status = 'Building', SchemaVersion = @SchemaVersion
WHEN NOT MATCHED THEN INSERT (Id, SchemaVersion, Status) VALUES (1, @SchemaVersion, 'Building');

-- Radantallet leses HER, før aggregeringene, ikke ved «Ready» til slutt. Uttrykket er
-- det samme som CountCacheSourceRows.Sql leser det med, og oppslaget nekter å bruke en
-- buffer der de to tallene spriker.
--
-- Tidspunktet er det som gjør sjekken til et vern. Endres indekstabellen MENS bygget
-- går, har bufferen tall fra de gamle dataene — leses radantallet etterpå, lagres det
-- NYE tallet, de to stemmer, og bufferen blir brukt. Altså feil vei. Lest her er tallet
-- fra dataene bufferen faktisk ble bygget fra, og en endring underveis gir et avvik som
-- forkaster bufferen.
--
-- sys.partitions og ikke sys.dm_db_partition_stats: samme tall, men DMV-en krever
-- VIEW DATABASE STATE, som db_datareader ikke gir. Se CountCacheSourceRows.
DECLARE @SourceRows BIGINT = (
    SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS BIGINT)
    FROM sys.partitions p
    WHERE p.object_id = OBJECT_ID('dbo.ObservationEntityIndex')
      AND p.index_id IN (0, 1));

-- Tømmer bare det nivået som faktisk bygges, slik at nivåene kan bygges hver for seg.
-- Medlemstabellen hører til nivå 1 fordi det er der flerverdi-bøttene defineres.
RAISERROR('Tømmer det som skal bygges...', 0, 1) WITH NOWAIT;
IF @BuildLevel1 = 1
BEGIN
    TRUNCATE TABLE dbo.AreaCountCacheLevel1;
    TRUNCATE TABLE dbo.AreaCountCacheBucketMember;
END
IF @BuildLevel2 = 1 TRUNCATE TABLE dbo.AreaCountCacheLevel2;

-- ---------------------------------------------------------------------------
-- Flerverdi-bøttene
--
-- Bøtta er HELE mengden en observasjon tilhører, ikke enkeltområdet. Uten det ville
-- en observasjon i to verneområder blitt talt to ganger når begge er valgt.
-- ---------------------------------------------------------------------------
-- Signaturen er den sorterte medlemslista. To observasjoner med samme signatur hører
-- til samme bøtte.
-- Medlems-id-en pakker omraadetypen inn: type * 1 000 000 + id. Kommune 4206 og
-- verneomraade 4206 er ulike omraader og maa ikke kollapse til samme medlem.
-- Speiler AreaCountCacheDimensions.PackAreaMember.
RAISERROR('Bygger geografi-boetter...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

SELECT ObservationId,
       CAST(STRING_AGG(CAST(EntityTypeId * 1000000 + EntityId AS VARCHAR(12)), ',')
            WITHIN GROUP (ORDER BY EntityTypeId, EntityId) AS NVARCHAR(4000)) AS Signatur
INTO #vernsig
FROM dbo.ObservationEntityIndex WHERE EntityTypeId IN (1, 2, 3, 4, 6)
GROUP BY ObservationId;

-- SIGNATUREN MAA IKKE VAERE AVKORTET.
--
-- CAST til en kort strengtype avkorter I STILLHET. Blir to ulike omraademengder
-- avkortet til samme tekst, faar de SAMME boette-id - og da summerer oppslaget
-- observasjoner som ikke hoerer sammen. Gale tall, ingen feilmelding, stivnet i
-- en tabell. ~9 tegn per medlem gir tak rundt 440 omraader paa en observasjon,
-- altsaa langt utenfor det plausible, men en stille sammenblanding er ikke noe
-- aa risikere paa et "burde ikke skje".
IF EXISTS (SELECT 1 FROM #vernsig WHERE LEN(Signatur) >= 3990)
BEGIN
    RAISERROR('Geografi-signaturen naermer seg 4000 tegn og kan vaere avkortet. Bufferen bygges IKKE. Utvid Signatur eller bytt til en HASHBYTES-signatur.', 16, 1);
    RETURN;
END

CREATE TABLE #vernbucket (
    -- COLLATE DATABASE_DEFAULT: en CREATE TABLE-kolonne i tempdb arver tempdbs
    -- sortering, mens SELECT INTO arver databasens. Uten dette kolliderer de i joinen.
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

IF @BuildLevel1 = 1
BEGIN
    INSERT #vernbucket (Signatur, BucketId)
    SELECT Signatur, CAST(ROW_NUMBER() OVER (ORDER BY Signatur) AS INT)
    FROM (SELECT DISTINCT Signatur FROM #vernsig) q;

    INSERT dbo.AreaCountCacheBucketMember (DimensionId, MemberId, BucketId)
    SELECT DISTINCT 12, CAST(v.value AS INT), b.BucketId
    FROM #vernbucket b CROSS APPLY STRING_SPLIT(b.Signatur, ',') v;
END
ELSE
BEGIN
    -- Nivå 2 bygges alene: bøtte-id-ene MÅ være de samme som nivå 1 bruker, ellers
    -- peker de to nivåene på hver sine bøtter. Derfor leses de tilbake fra
    -- medlemstabellen framfor å nummereres på nytt.
    INSERT #vernbucket (Signatur, BucketId)
    SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
    FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 12 GROUP BY BucketId;
END


SELECT s.ObservationId, b.BucketId
INTO #vern FROM #vernsig s JOIN #vernbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #vern(ObservationId);

SET @Msg = CONCAT('  geografi: ', FORMAT((SELECT COUNT(*) FROM #vernbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

RAISERROR('Bygger prosjekt-boetter...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

SELECT ObservationId,
       CAST(STRING_AGG(CAST(ProjectOrgId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY ProjectOrgId) AS NVARCHAR(4000)) AS Signatur
INTO #projsig
FROM dbo.ObservationProject GROUP BY ObservationId;

-- Samme vakt som for geografi-signaturen over, av samme grunn.
IF EXISTS (SELECT 1 FROM #projsig WHERE LEN(Signatur) >= 3990)
BEGIN
    RAISERROR('Prosjekt-signaturen naermer seg 4000 tegn og kan vaere avkortet. Bufferen bygges IKKE. Utvid Signatur eller bytt til en HASHBYTES-signatur.', 16, 1);
    RETURN;
END

CREATE TABLE #projbucket (
    -- COLLATE DATABASE_DEFAULT: en CREATE TABLE-kolonne i tempdb arver tempdbs
    -- sortering, mens SELECT INTO arver databasens. Uten dette kolliderer de i joinen.
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

IF @BuildLevel1 = 1
BEGIN
    INSERT #projbucket (Signatur, BucketId)
    SELECT Signatur, CAST(ROW_NUMBER() OVER (ORDER BY Signatur) AS INT)
    FROM (SELECT DISTINCT Signatur FROM #projsig) q;

    INSERT dbo.AreaCountCacheBucketMember (DimensionId, MemberId, BucketId)
    SELECT DISTINCT 13, CAST(v.value AS INT), b.BucketId
    FROM #projbucket b CROSS APPLY STRING_SPLIT(b.Signatur, ',') v;
END
ELSE
BEGIN
    INSERT #projbucket (Signatur, BucketId)
    SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
    FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 13 GROUP BY BucketId;
END


SELECT s.ObservationId, b.BucketId
INTO #proj FROM #projsig s JOIN #projbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #proj(ObservationId);

SET @Msg = CONCAT('  prosjekt: ', FORMAT((SELECT COUNT(*) FROM #projbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Nivå 1
-- ---------------------------------------------------------------------------
DECLARE @Id TINYINT, @Navn VARCHAR(40), @Uttrykk NVARCHAR(200), @Sql NVARCHAR(MAX), @Join NVARCHAR(400);

IF @BuildLevel1 = 1
BEGIN
    RAISERROR('--- Nivaa 1 ---', 0, 1) WITH NOWAIT;
    DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT Id, Navn, Uttrykk FROM #dim ORDER BY Id;
    OPEN c1; FETCH NEXT FROM c1 INTO @Id, @Navn, @Uttrykk;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @T = SYSUTCDATETIME();
        SET @Join = CASE WHEN @Id = 12 THEN @JoinVern WHEN @Id = 13 THEN @JoinProj ELSE N'' END;

        SET @Sql = N'
INSERT dbo.AreaCountCacheLevel1 (DimensionId, BucketId, EntityTypeId, EntityId, Mask, ObservationCount)
SELECT ' + CAST(@Id AS VARCHAR(3)) + N', ' + @Uttrykk + N', i.EntityTypeId, i.EntityId, CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16, COUNT_BIG(*)
FROM dbo.ObservationEntityIndex i' + @Join + N'
WHERE ' + @Uttrykk + N' <> -2147483648
GROUP BY ' + @Uttrykk + N', i.EntityTypeId, i.EntityId, CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16
OPTION (RECOMPILE);';
        EXEC sp_executesql @Sql;
        SET @Rows = @@ROWCOUNT;

        SET @Msg = CONCAT('  ', @Navn, ': ', FORMAT(@Rows, 'N0'), ' rader, ',
                          DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        FETCH NEXT FROM c1 INTO @Id, @Navn, @Uttrykk;
    END
    CLOSE c1; DEALLOCATE c1;
END

-- ---------------------------------------------------------------------------
-- Nivå 2
-- ---------------------------------------------------------------------------
IF @BuildLevel2 = 1
BEGIN
    RAISERROR('--- Nivaa 2 (66 par) ---', 0, 1) WITH NOWAIT;

    SELECT a.Id AS IdA, b.Id AS IdB, a.Navn AS NavnA, b.Navn AS NavnB,
           a.Uttrykk AS UttrykkA, b.Uttrykk AS UttrykkB,
           CAST(ROW_NUMBER() OVER (ORDER BY a.Id, b.Id) AS TINYINT) AS PairId
    INTO #par
    FROM #dim a JOIN #dim b ON b.Id > a.Id;

    DECLARE @IdA TINYINT, @IdB TINYINT, @NavnA VARCHAR(40), @NavnB VARCHAR(40),
            @UttA NVARCHAR(200), @UttB NVARCHAR(200), @PairId TINYINT;

    DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT IdA, IdB, NavnA, NavnB, UttrykkA, UttrykkB, PairId FROM #par ORDER BY PairId;
    OPEN c2; FETCH NEXT FROM c2 INTO @IdA, @IdB, @NavnA, @NavnB, @UttA, @UttB, @PairId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @T = SYSUTCDATETIME();
        SET @Join = CASE WHEN @IdA = 12 OR @IdB = 12 THEN @JoinVern ELSE N'' END
                  + CASE WHEN @IdA = 13 OR @IdB = 13 THEN @JoinProj ELSE N'' END;

        SET @Sql = N'
INSERT dbo.AreaCountCacheLevel2 (DimensionPairId, BucketA, BucketB, EntityTypeId, EntityId, Mask, ObservationCount)
SELECT ' + CAST(@PairId AS VARCHAR(3)) + N', ' + @UttA + N', ' + @UttB + N', i.EntityTypeId, i.EntityId, CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16, COUNT_BIG(*)
FROM dbo.ObservationEntityIndex i' + @Join + N'
WHERE ' + @UttA + N' <> -2147483648 AND ' + @UttB + N' <> -2147483648
GROUP BY ' + @UttA + N', ' + @UttB + N', i.EntityTypeId, i.EntityId, CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16
OPTION (RECOMPILE);';
        EXEC sp_executesql @Sql;
        SET @Rows = @@ROWCOUNT;

        SET @Msg = CONCAT('  [', @PairId, '/66] ', @NavnA, ' x ', @NavnB, ': ',
                          FORMAT(@Rows, 'N0'), ' rader, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's',
                          ' | totalt ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        FETCH NEXT FROM c2 INTO @IdA, @IdB, @NavnA, @NavnB, @UttA, @UttB, @PairId;
    END
    CLOSE c2; DEALLOCATE c2;
END

-- ---------------------------------------------------------------------------
-- Ferdig
-- ---------------------------------------------------------------------------
UPDATE STATISTICS dbo.AreaCountCacheLevel1;
UPDATE STATISTICS dbo.AreaCountCacheLevel2;
UPDATE STATISTICS dbo.AreaCountCacheBucketMember;

-- «Ready» BARE naar BEGGE nivaaene er bygget i denne kjoeringen.
--
-- Overskriften inviterer til aa bygge ett nivaa om gangen, og tak-flaggene gater
-- baade toemmingen og hver byggeblokk - men stemplet gjorde de ikke. Kjoerte man
-- @BuildLevel1 = 0, @BuildLevel2 = 1 etter en import, ble nivaa 2 ferskt, nivaa 1 og
-- medlemstabellen sto igjen fra forrige datasett, og SourceRows ble satt til dagens
-- radantall. Da gaar ferskhetsvakten i AreaCountCacheService god for BEGGE nivaaene,
-- og hvert ett-filter-kall - det vanlige tilfellet - svarer fra gamle tall.
--
-- @SourceRows ble lest FOER aggregeringene, se der. Skrives tallet som leses her,
-- ville en endring i indekstabellen underveis gitt en buffer med gamle tall og nytt
-- radantall - altsaa en buffer oppslaget tror er fersk.
IF @BuildLevel1 = 1 AND @BuildLevel2 = 1
BEGIN
    UPDATE dbo.AreaCountCacheState
    SET Status = 'Ready',
        BuiltAt = SYSUTCDATETIME(),
        SourceRows = @SourceRows,
        DurationSeconds = DATEDIFF(SECOND, @Start, SYSUTCDATETIME())
    WHERE Id = 1;
END
ELSE
BEGIN
    SET @Msg = CONCAT('Bare ett nivaa bygget (nivaa 1: ', @BuildLevel1,
                      ', nivaa 2: ', @BuildLevel2, '). Status forblir Building, og ',
                      'bufferen brukes IKKE. Kjoer med begge flaggene satt for aa ',
                      'stemple den klar.');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
END

SELECT 'Nivaa 1' AS Tabell, FORMAT(COUNT_BIG(*), 'N0') AS Rader FROM dbo.AreaCountCacheLevel1
UNION ALL SELECT 'Nivaa 2', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.AreaCountCacheLevel2
UNION ALL SELECT 'Medlemmer', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.AreaCountCacheBucketMember;

SET @Msg = CONCAT('Ferdig paa ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

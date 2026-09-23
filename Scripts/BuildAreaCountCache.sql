/*
    Bygger områdebufferen (AreaCountCacheLevel1 / -Level2 / -BucketMember / -State).

    MIDLERTIDIG. Dette er en manuell erstatning for byggejobben i fase 3, laget for å
    kunne måle effekten før jobben finnes. Den har ikke blue/green, ikke resumering og
    ikke verifisering — den tømmer og bygger på nytt. Ikke kjør den mot produksjon.

    BØTTEDEFINISJONENE MÅ STEMME MED AreaCountCacheDimensions.
    Dimensjons-id-ene, NULL-bøtta og pargruppe-rekkefølgen er duplisert her. Endres
    registeret i C#, må dette skriptet endres i takt — og SchemaVersion under må
    oppdateres, ellers nekter oppslaget å bruke bufferen (som er poenget med den).

    PARGRUPPE-ID
    Tildeles som i C#: id-ene sortert stigende, deretter alle par (i, j) med i < j i
    den rekkefølgen, nummerert fra 1. ROW_NUMBER OVER (ORDER BY a.Id, b.Id) gir
    nøyaktig samme sekvens.

    KJØRING
    Sett @BuildLevel1 / @BuildLevel2 for å bygge ett nivå om gangen. Nivå 2 er 78
    grupperinger over 134 millioner rader og ~36 millioner rader å sette inn.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BuildLevel1 BIT = 1;
DECLARE @BuildLevel2 BIT = 1;

-- Hentet fra AreaCountCacheDimensions.SchemaVersion. Stemmer ikke denne med koden,
-- ignoreres bufferen — med vilje.
DECLARE @SchemaVersion INT = 1194527040;

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
 (1,  'Bilder',         N'CASE WHEN i.HasMediaFiles = 1 THEN 1 ELSE 0 END'),
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

-- Tømmer bare det nivået som faktisk bygges, slik at nivåene kan bygges hver for seg.
-- Medlemstabellen hører til nivå 1 fordi det er der flerverdi-bøttene defineres.
RAISERROR('Tømmer det som skal bygges...', 0, 1) WITH NOWAIT;
IF @BuildLevel1 = 1
BEGIN
    TRUNCATE TABLE dbo.AreaCountCacheLevel1;
    TRUNCATE TABLE dbo.AreaCountCacheBucketMember;
END
IF @BuildLevel2 = 1 TRUNCATE TABLE dbo.AreaCountCacheLevel2;

-- Status settes til Building mens vi holder på. Oppslaget krever Ready, så en avbrutt
-- kjøring etterlater en buffer som ikke brukes — framfor en halv buffer som brukes.
MERGE dbo.AreaCountCacheState AS t
USING (SELECT CAST(1 AS TINYINT) AS Id) AS s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET Status = 'Building', SchemaVersion = @SchemaVersion
WHEN NOT MATCHED THEN INSERT (Id, SchemaVersion, Status) VALUES (1, @SchemaVersion, 'Building');

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
INSERT dbo.AreaCountCacheLevel1 (DimensionId, BucketId, EntityTypeId, EntityId, ObservationCount)
SELECT ' + CAST(@Id AS VARCHAR(3)) + N', ' + @Uttrykk + N', i.EntityTypeId, i.EntityId, COUNT_BIG(*)
FROM dbo.ObservationEntityIndex i' + @Join + N'
WHERE ' + @Uttrykk + N' <> -2147483648
GROUP BY ' + @Uttrykk + N', i.EntityTypeId, i.EntityId
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
    RAISERROR('--- Nivaa 2 (78 par) ---', 0, 1) WITH NOWAIT;

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
INSERT dbo.AreaCountCacheLevel2 (DimensionPairId, BucketA, BucketB, EntityTypeId, EntityId, ObservationCount)
SELECT ' + CAST(@PairId AS VARCHAR(3)) + N', ' + @UttA + N', ' + @UttB + N', i.EntityTypeId, i.EntityId, COUNT_BIG(*)
FROM dbo.ObservationEntityIndex i' + @Join + N'
WHERE ' + @UttA + N' <> -2147483648 AND ' + @UttB + N' <> -2147483648
GROUP BY ' + @UttA + N', ' + @UttB + N', i.EntityTypeId, i.EntityId
OPTION (RECOMPILE);';
        EXEC sp_executesql @Sql;
        SET @Rows = @@ROWCOUNT;

        SET @Msg = CONCAT('  [', @PairId, '/78] ', @NavnA, ' x ', @NavnB, ': ',
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

UPDATE dbo.AreaCountCacheState
SET Status = 'Ready',
    BuiltAt = SYSUTCDATETIME(),
    SourceRows = (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex),
    DurationSeconds = DATEDIFF(SECOND, @Start, SYSUTCDATETIME())
WHERE Id = 1;

SELECT 'Nivaa 1' AS Tabell, FORMAT(COUNT_BIG(*), 'N0') AS Rader FROM dbo.AreaCountCacheLevel1
UNION ALL SELECT 'Nivaa 2', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.AreaCountCacheLevel2
UNION ALL SELECT 'Medlemmer', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.AreaCountCacheBucketMember;

SET @Msg = CONCAT('Ferdig paa ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

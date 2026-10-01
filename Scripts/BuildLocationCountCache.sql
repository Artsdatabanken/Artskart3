/*
    Bygger lokasjonsbufferen (LocationCountCacheLevel1 / -State).

    MIDLERTIDIG. Dette er en manuell erstatning for byggejobben, laget for å kunne
    måle effekten før jobben finnes. Den har ikke blue/green, ikke resumering og ikke
    verifisering — den tømmer og bygger på nytt. Ikke kjør den mot produksjon.

    BØTTEDEFINISJONENE MÅ STEMME MED AreaCountCacheDimensions.
    Uttrykkene under er de samme som i BuildAreaCountCache.sql, og skal være det:
    bøttingen er identisk, bare grupperingsnøkkelen er en annen. Endres registeret i
    C#, må BEGGE skriptene endres i takt, og @SchemaVersion oppdateres — ellers nekter
    oppslaget å bruke bufferen, som er poenget med den.

    FORUTSETTER AreaCountCacheBucketMember
    Dimensjon 12 (geografi) og 13 (prosjekt) er flerverdi, og bøtte-id-ene deres slås
    opp i medlemstabellen som områdebufferen eier. Kjør BuildAreaCountCache.sql med
    @BuildLevel1 = 1 først. Skriptet stopper med en tydelig feil hvis den er tom —
    ikke fordi det ville krasjet, men fordi det ville bygget en buffer der et
    fylkesvalg stille ga et tomt kart.

    HVORFOR COUNT(DISTINCT ObservationId)
    Én observasjon har én rad per område den ligger i — i snitt drøyt to. Grupperer
    man på lokasjon, havner alle de radene i samme gruppe, og COUNT(*) ville talt
    områdemedlemskap i stedet for observasjoner. Områdebufferen kan bruke COUNT_BIG(*)
    fordi den grupperer PÅ området; det kan ikke denne.

    Den indre DISTINCT gjør den ytre tellingen billig: 134 millioner indeksrader
    kollapser til rundt 61 millioner (observasjon, lokasjon, bøtte) før aggregeringen.

    KJØRING
    Fjorten grupperinger over 134 millioner rader. Regn med at den tar en stund.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Hentet fra AreaCountCacheDimensions.SchemaVersion. Stemmer ikke denne med koden,
-- ignoreres bufferen — med vilje.
DECLARE @SchemaVersion INT = 1194527040;

DECLARE @NullBucket INT = -2147483648;   -- int.MinValue
DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @T DATETIME2, @Msg NVARCHAR(400), @Rows BIGINT;

-- ---------------------------------------------------------------------------
-- Dimensjonsregisteret, speilet fra C#
--
-- Uttrykket er hva raden skal bøttes på. Id 0 er lokasjonsbufferens egen:
-- «ingen filter». Den finnes ikke i AreaCountCacheDimensions fordi områdebufferen
-- aldri spørres uten filter — men her er det det tregeste kallet vi har målt, og
-- én bøtte per lokasjon koster bare ~5,1 millioner rader.
--
-- NULL-BØTTA BYGGES IKKE
-- Kolonner som kan være NULL foldes til sentinelen -2147483648, og rader med den
-- verdien filtreres BORT før innsetting. De kan aldri velges: et filter peker alltid
-- ut konkrete verdier, og intervalldimensjonene utelukker sentinelen eksplisitt i
-- LocationCountCacheService.
--
-- Sentinelen står likevel i uttrykkene, som vakt mot at en NULL skal nå en NOT NULL-
-- kolonne og velte hele bygget.
-- ---------------------------------------------------------------------------
CREATE TABLE #dim (Id TINYINT PRIMARY KEY, Navn VARCHAR(40), Uttrykk NVARCHAR(200));
INSERT #dim VALUES
 (0,  'Ufiltrert',      N'0'),
 (1,  'Bilder',         N'CASE WHEN i.HasMediaFiles = 1 THEN 1 ELSE 0 END'),
 (2,  'Regstatus',      N'CAST(i.RegistrationStatusId AS INT)'),
 (3,  'Atferd',         N'ISNULL(CAST(i.BehaviorId AS INT), -2147483648)'),
 (4,  'Funntype',       N'i.BasisOfRecordId'),
 (5,  'Kategori',       N'ISNULL(i.CategoryId, -2147483648)'),
 (6,  'Institusjon',    N'ISNULL(i.InstitutionOrgId, -2147483648)'),
 (7,  'Taksongruppe',   N'i.TaxonGroupId'),
 (8,  'Takson',         N'ISNULL(i.OrderTaxonId, -2147483648)'),
 (9,  'Datasett',       N'ISNULL(i.DatasetOrgId, -2147483648)'),
 -- Ordrett det samme uttrykket som BuildAreaCountCache.sql, MONTH() og alt. Den
 -- denormaliserte MonthCollected-kolonnen ville vaert billigere, men boette-id-ene
 -- MAA bli identiske med omraadebufferens, og to uttrykk som skal gi samme tall er
 -- to uttrykk som en dag ikke gjoer det. Dette bygges en gang.
 (10, 'Periode',        N'ISNULL(YEAR(i.DateTimeCollected) * 100 + MONTH(i.DateTimeCollected), -2147483648)'),
 (11, 'Koordpresisjon', N'ISNULL(i.CoordinatePrecisionInMeters, -2147483648)'),
 (12, 'Geografi',       N'ISNULL(v.BucketId, -2147483648)'),
 (13, 'Prosjekt',       N'ISNULL(p.BucketId, -2147483648)');

-- Joinene som trengs når en dimensjon er flerverdi. Settes inn i spørringen bare
-- når dimensjonen faktisk er med, så de tolv andre slipper kostnaden.
DECLARE @JoinVern NVARCHAR(200) = N' LEFT JOIN #vern v ON v.ObservationId = i.ObservationId';
DECLARE @JoinProj NVARCHAR(200) = N' LEFT JOIN #proj p ON p.ObservationId = i.ObservationId';

-- ---------------------------------------------------------------------------
-- Medlemstabellen må finnes
--
-- Uten den blir bøtte-id-ene for geografi og prosjekt NULL, alt havner i
-- NULL-bøtta og blir filtrert bort. Bufferen ville da sett ferdig ut og svart
-- «ingen lokasjoner» på hvert eneste områdefilter. Stopp heller her.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 12)
BEGIN
    RAISERROR('AreaCountCacheBucketMember mangler dimensjon 12 (geografi). Kjoer BuildAreaCountCache.sql med @BuildLevel1 = 1 foerst.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 13)
BEGIN
    RAISERROR('AreaCountCacheBucketMember mangler dimensjon 13 (prosjekt). Kjoer BuildAreaCountCache.sql med @BuildLevel1 = 1 foerst.', 16, 1);
    RETURN;
END

RAISERROR('Toemmer lokasjonsbufferen...', 0, 1) WITH NOWAIT;
TRUNCATE TABLE dbo.LocationCountCacheLevel1;

-- Status settes til Building mens vi holder på. Oppslaget krever Ready, så en avbrutt
-- kjøring etterlater en buffer som ikke brukes — framfor en halv buffer som brukes.
MERGE dbo.LocationCountCacheState AS t
USING (SELECT CAST(1 AS TINYINT) AS Id) AS s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET Status = 'Building', SchemaVersion = @SchemaVersion
WHEN NOT MATCHED THEN INSERT (Id, SchemaVersion, Status) VALUES (1, @SchemaVersion, 'Building');

-- ---------------------------------------------------------------------------
-- Flerverdi-bøttene leses TILBAKE fra medlemstabellen
--
-- De nummereres ikke på nytt. Bøtte-id-ene må være nøyaktig de samme som
-- områdebufferen bruker, ellers peker de to bufferne på hver sine bøtter — og
-- LocationCountCacheService slår opp medlemmene i nettopp den tabellen.
-- ---------------------------------------------------------------------------
RAISERROR('Leser geografi-boetter fra medlemstabellen...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

CREATE TABLE #vernbucket (
    -- COLLATE DATABASE_DEFAULT: en CREATE TABLE-kolonne i tempdb arver tempdbs
    -- sortering, mens SELECT INTO arver databasens. Uten dette kolliderer de i joinen.
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

INSERT #vernbucket (Signatur, BucketId)
SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 12 GROUP BY BucketId;

SELECT ObservationId,
       CAST(STRING_AGG(CAST(EntityTypeId * 1000000 + EntityId AS VARCHAR(12)), ',')
            WITHIN GROUP (ORDER BY EntityTypeId, EntityId) AS NVARCHAR(4000)) AS Signatur
INTO #vernsig
FROM dbo.ObservationEntityIndex WHERE EntityTypeId IN (1, 2, 3, 4, 6)
GROUP BY ObservationId;

SELECT s.ObservationId, b.BucketId
INTO #vern FROM #vernsig s JOIN #vernbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #vern(ObservationId);

SET @Msg = CONCAT('  geografi: ', FORMAT((SELECT COUNT(*) FROM #vernbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

RAISERROR('Leser prosjekt-boetter fra medlemstabellen...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

CREATE TABLE #projbucket (
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

INSERT #projbucket (Signatur, BucketId)
SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 13 GROUP BY BucketId;

SELECT ObservationId,
       CAST(STRING_AGG(CAST(ProjectOrgId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY ProjectOrgId) AS NVARCHAR(4000)) AS Signatur
INTO #projsig
FROM dbo.ObservationProject GROUP BY ObservationId;

SELECT s.ObservationId, b.BucketId
INTO #proj FROM #projsig s JOIN #projbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #proj(ObservationId);

SET @Msg = CONCAT('  prosjekt: ', FORMAT((SELECT COUNT(*) FROM #projbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Bygget
--
-- KILDEN MÅ VÆRE DEN SAMME SOM TELLINGEN BRUKER
-- Predikatet «EntityTypeId <> 101 AND LocationId IS NOT NULL» er kopiert fra
-- GetLocationsAsync og skal bli stående der det er. Bufferen skal gi NØYAKTIG de
-- samme tallene som tellingen, ikke bedre tall: teller vi fra Observation i stedet,
-- kommer de 722 048 observasjonene som mangler indeksrader plutselig med, og de to
-- stiene svarer ulikt avhengig av om bufferen er bygget. Skulle det hullet tettes en
-- dag, endres begge samtidig.
--
-- 101 er institusjonsrader, ikke områder.
-- ---------------------------------------------------------------------------
DECLARE @Id TINYINT, @Navn VARCHAR(40), @Uttrykk NVARCHAR(200), @Sql NVARCHAR(MAX), @Join NVARCHAR(400);

RAISERROR('--- Bygger 14 dimensjoner ---', 0, 1) WITH NOWAIT;
DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT Id, Navn, Uttrykk FROM #dim ORDER BY Id;
OPEN c1; FETCH NEXT FROM c1 INTO @Id, @Navn, @Uttrykk;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @T = SYSUTCDATETIME();
    SET @Join = CASE WHEN @Id = 12 THEN @JoinVern WHEN @Id = 13 THEN @JoinProj ELSE N'' END;

    SET @Sql = N'
INSERT dbo.LocationCountCacheLevel1 (DimensionId, BucketId, LocationId, East, North, ObservationCount)
SELECT ' + CAST(@Id AS VARCHAR(3)) + N', q.Bucket, q.LocationId, l.East, l.North, COUNT(DISTINCT q.ObservationId)
FROM (
    SELECT DISTINCT i.ObservationId, i.LocationId, ' + @Uttrykk + N' AS Bucket
    FROM dbo.ObservationEntityIndex i' + @Join + N'
    WHERE i.EntityTypeId <> 101 AND i.LocationId IS NOT NULL
) q
JOIN dbo.Location l ON l.Id = q.LocationId
WHERE q.Bucket <> -2147483648
GROUP BY q.Bucket, q.LocationId, l.East, l.North
OPTION (RECOMPILE);';
    EXEC sp_executesql @Sql;
    SET @Rows = @@ROWCOUNT;

    SET @Msg = CONCAT('  ', @Navn, ': ', FORMAT(@Rows, 'N0'), ' rader, ',
                      DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's',
                      ' | totalt ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    FETCH NEXT FROM c1 INTO @Id, @Navn, @Uttrykk;
END
CLOSE c1; DEALLOCATE c1;

-- ---------------------------------------------------------------------------
-- Ferdig
-- ---------------------------------------------------------------------------
UPDATE STATISTICS dbo.LocationCountCacheLevel1;

UPDATE dbo.LocationCountCacheState
SET Status = 'Ready',
    BuiltAt = SYSUTCDATETIME(),
    SourceRows = (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex),
    DurationSeconds = DATEDIFF(SECOND, @Start, SYSUTCDATETIME())
WHERE Id = 1;

SELECT FORMAT(COUNT_BIG(*), 'N0') AS TotaltAntallRader FROM dbo.LocationCountCacheLevel1;

SELECT d.Navn,
       FORMAT(COUNT_BIG(c.LocationId), 'N0') AS Rader
FROM #dim d
LEFT JOIN dbo.LocationCountCacheLevel1 c ON c.DimensionId = d.Id
GROUP BY d.Id, d.Navn ORDER BY d.Id;

SET @Msg = CONCAT('Ferdig paa ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

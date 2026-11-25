/*
    Bygger fasett-bufferen (FacetCountCacheLevel0 / -Level1 / -Level2 / -State).

    Fasett-tallene er antall observasjoner ved hvert valg i filterpanelet. En
    fasett-telling er en områdetelling uten område: samme bøtter, samme maske og
    samme pargrupper som områdebufferen, bare uten utdatacellen.

    ÉN RAD PER OBSERVASJON FØRST, AGGREGERINGENE ETTERPÅ
    Indekstabellen har én rad per (observasjon, områdetype) — 134,7 mill. rader over
    60,3 mill. observasjoner. Uten utdatacellen å gruppere på måtte hver aggregering
    ellers telt COUNT(DISTINCT ObservationId), og da er det DISTINCT-en som koster:
    målt 11-13 s per aggregering, gjort 79 ganger. Dedupliseres det i stedet ÉN gang
    inn i #obs, trenger ingen aggregering DISTINCT. Målt:

      materialisere #obs                 57 s
      bygge columnstore paa #obs         99 s
      nivaa 1 fra columnstore            74 ms     (mot 11-13 s med DISTINCT)
      nivaa 2, kategori x periode        669 ms
      samme fra heap, uten columnstore   4,3 s / 7,7 s

    Verifisert at de to formene gir identiske tall.

    FORUTSETTER AT OMRÅDEBUFFEREN ER BYGGET
    Flerverdi-bøttene (geografi, prosjekt) har syntetiske id-er, og de MÅ være de
    samme her som i områdebufferen — begge leser AreaCountCacheBucketMember ved
    oppslag. Skriptet nummererer dem derfor ikke på nytt, men leser dem tilbake fra
    medlemstabellen. Er den tom, stopper bygget: å nummerere på nytt kunne gitt andre
    id-er, og da ville fasett-tallene svart for feil bøtte uten at noe feilet.

    ALLE TRE NIVÅENE BYGGES I SAMME KJØRING
    Ingen flagg for å bygge ett nivå om gangen, i motsetning til
    BuildAreaCountCache.sql. Hele bygget er målt til under fire minutter, så
    delkjøringer er ikke verdt fella de åpner: et halvbygget nivå fra et eldre
    datasett sammen med ferskt radantall gir en buffer ferskhetsvakten går god for.

    TØMMER OG BYGGER PÅ NYTT
    Ingen blue/green. Bufferen er borte i de minuttene bygget tar, og fasett-tallene
    faller da tilbake til aggregering — samme forbehold som områdebufferen har.
    Status settes til «Building» før tømmingen, så et avbrudd etterlater en ikke-klar
    buffer framfor en halv buffer som leses.

    BØTTEDEFINISJONENE MÅ STEMME MED AreaCountCacheDimensions OG FilterMask.
    Dimensjons-id-ene, uttrykkene, NULL-bøtta, maskeuttrykket og pargruppe-
    rekkefølgen er duplisert her. Uttrykkene er ORDRETT de samme som i
    BuildAreaCountCache.sql — CountCacheScriptSchemaVersionTests sammenligner dem, så
    en endring i det ene skriptet uten det andre feiler. @SchemaVersion må også
    oppdateres, ellers nekter oppslaget å bruke bufferen (som er poenget med den).

    PARGRUPPE-ID
    Tildeles som i C#: id-ene sortert stigende, deretter alle par (i, j) med i < j i
    den rekkefølgen, nummerert fra 1. ROW_NUMBER OVER (ORDER BY a.Id, b.Id) gir
    nøyaktig samme sekvens. Identisk med BuildAreaCountCache.sql, med vilje.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Temptabellene ryddes først: en #temp-tabell lever til sesjonen lukkes, ikke til
-- batchen slutter. Kjøres dette skriptet på samme tilkobling som
-- BuildAreaCountCache.sql, deler de flere navn.
DROP TABLE IF EXISTS #dim, #par, #obs, #vernsig, #vernbucket, #vern, #projsig, #projbucket, #proj;

-- Hentet fra FacetCountCacheDimensions.SchemaVersion. Stemmer ikke denne med koden,
-- ignoreres bufferen — med vilje.
DECLARE @SchemaVersion INT = 229709519;

DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @T DATETIME2, @Msg NVARCHAR(400), @Rows BIGINT, @Sql NVARCHAR(MAX);

-- ---------------------------------------------------------------------------
-- Dimensjonsregisteret, speilet fra C#
--
-- Uttrykk er hva raden skal bøttes på, lest fra indekstabellen med alias i — ordrett
-- som i BuildAreaCountCache.sql. Navn er BÅDE dimensjonsnavnet og kolonnenavnet i
-- #obs, så uttrykket står ett sted og aggregeringene under refererer bare kolonnen.
--
-- ALLE TOLV DIMENSJONENE, IKKE BARE DE SEKS FASETTENE
-- Bare seks dimensjoner kan VISES som fasett, men alle tolv kan BEGRENSE en fasett.
-- Et bool-alternativ med ett aktivt filter leses fra nivå 1 for det filterets
-- dimensjon, og med to aktive filtre fra nivå 2 for det paret. Nivå 1 er 68 834
-- rader i alt, så det er ingenting å spare på å utelate noen.
--
-- NULL-BØTTA BYGGES IKKE
-- Kolonner som kan være NULL foldes til sentinelen -2147483648, og rader med den
-- verdien filtreres BORT før innsetting. De kan aldri velges: et filter peker alltid
-- ut konkrete verdier, og intervalldimensjonene utelukker sentinelen eksplisitt ved
-- oppslag. Samme regel som områdebufferen.
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
 (12, 'Geografi',       N'ISNULL(v.BucketId, -2147483648)'),
 (13, 'Prosjekt',       N'ISNULL(p.BucketId, -2147483648)');

-- Maskeuttrykket MÅ stemme med FilterMask.SqlExpression. Står ett sted, ikke én gang
-- per aggregering som i områdeskriptet, fordi masken blir en kolonne i #obs.
DECLARE @MaskUttrykk NVARCHAR(400) = N'CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 + CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 + CAST(i.HasMediaFiles AS INT) * 16';

-- ---------------------------------------------------------------------------
-- Forutsetning: områdebufferens medlemstabell
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 12)
    OR NOT EXISTS (SELECT 1 FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 13)
BEGIN
    RAISERROR('AreaCountCacheBucketMember mangler rader for geografi (12) og/eller prosjekt (13). Fasett-bufferen henter boette-id-ene derfra for aa vaere identisk med omraadebufferen, og kan ikke bygges foer BuildAreaCountCache.sql har kjoert. Bufferen bygges IKKE.', 16, 1);
    RETURN;
END

-- ---------------------------------------------------------------------------
-- MonthCollected MÅ stemme med MONTH(DateTimeCollected)
--
-- Samme invariant som BuildAreaCountCache.sql sjekker, og av samme grunn:
-- periodedimensjonen bøttes på YEAR * 100 + MONTH(DateTimeCollected), mens tellingen
-- filtrerer på MonthCollected. Spriker de to, gir bufferen andre tall enn tellingen —
-- og kontrakten er at den skal gi nøyaktig det tellingen gir.
--
-- Står FØR Status settes til Building: en buffer som finnes og er i orden skal ikke
-- slås av fordi en forutsetning ikke holder.
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
                      'Periodefasetten ville gitt andre tall enn tellingen. Bufferen ',
                      'bygges IKKE - reparer kolonnen og proev paa nytt.');
    RAISERROR(@Msg, 16, 1);
    RETURN;
END

-- Status settes til Building FØR tømmingen. Hver setning er sin egen transaksjon
-- (XACT_ABORT uten omsluttende transaksjon), så feiler tømmingen halvveis, er
-- bufferen allerede merket ikke-klar. Settes statusen etterpå, etterlater et avbrudd
-- en TOM buffer som fortsatt leses som Ready — null ved hvert filtervalg, status 200,
-- ingen feilmelding.
MERGE dbo.FacetCountCacheState AS t
USING (SELECT CAST(1 AS TINYINT) AS Id) AS s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET Status = 'Building', SchemaVersion = @SchemaVersion
WHEN NOT MATCHED THEN INSERT (Id, SchemaVersion, Status) VALUES (1, @SchemaVersion, 'Building');

-- Radantallet leses HER, før aggregeringene, ikke ved «Ready» til slutt. Endres
-- indekstabellen MENS bygget går, har bufferen tall fra de gamle dataene — leses
-- radantallet etterpå, lagres det NYE tallet, de to stemmer, og bufferen blir brukt.
-- Altså feil vei. Se BuildAreaCountCache.sql og CountCacheSourceRows.
DECLARE @SourceRows BIGINT = (
    SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS BIGINT)
    FROM sys.partitions p
    WHERE p.object_id = OBJECT_ID('dbo.ObservationEntityIndex')
      AND p.index_id IN (0, 1));

RAISERROR('Toemmer fasett-bufferen...', 0, 1) WITH NOWAIT;
TRUNCATE TABLE dbo.FacetCountCacheLevel0;
TRUNCATE TABLE dbo.FacetCountCacheLevel1;
TRUNCATE TABLE dbo.FacetCountCacheLevel2;

-- ---------------------------------------------------------------------------
-- Flerverdi-bøttene, lest tilbake fra områdebufferens medlemstabell
--
-- Bøtta er HELE mengden en observasjon tilhører, ikke enkeltområdet. Uten det ville
-- en observasjon i to verneområder blitt talt to ganger når begge er valgt.
--
-- Signaturen rekonstrueres som den sorterte medlemslista, akkurat som
-- BuildAreaCountCache.sql gjør når bare nivå 2 bygges. Sortering på MemberId er den
-- samme som på (EntityTypeId, EntityId), siden MemberId = type * 1 000 000 + id og
-- største observerte id er 5636.
-- ---------------------------------------------------------------------------
RAISERROR('Leser geografi-boetter fra medlemstabellen...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

SELECT ObservationId,
       CAST(STRING_AGG(CAST(EntityTypeId * 1000000 + EntityId AS VARCHAR(12)), ',')
            WITHIN GROUP (ORDER BY EntityTypeId, EntityId) AS NVARCHAR(4000)) AS Signatur
INTO #vernsig
FROM dbo.ObservationEntityIndex WHERE EntityTypeId IN (1, 2, 3, 4, 6)
GROUP BY ObservationId;

-- CAST til en kort strengtype avkorter I STILLHET, og to ulike områdemengder som
-- avkortes til samme tekst får SAMME bøtte-id. Gale tall, ingen feilmelding.
IF EXISTS (SELECT 1 FROM #vernsig WHERE LEN(Signatur) >= 3990)
BEGIN
    RAISERROR('Geografi-signaturen naermer seg 4000 tegn og kan vaere avkortet. Bufferen bygges IKKE.', 16, 1);
    RETURN;
END

CREATE TABLE #vernbucket (
    -- COLLATE DATABASE_DEFAULT: en CREATE TABLE-kolonne i tempdb arver tempdbs
    -- sortering, mens SELECT INTO arver databasens. Uten dette kolliderer de i joinen.
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

INSERT #vernbucket (Signatur, BucketId)
SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 12 GROUP BY BucketId;

-- EN OBSERVASJON UTEN BØTTE ER EN MEDLEMSTABELL SOM IKKE PASSER DATAENE
--
-- Joinen under er en LEFT JOIN, så en manglende bøtte gir NULL-bøtta og observasjonen
-- faller stille ut av geografi-dimensjonen. Da blir fasett-tallene for lave for hvert
-- geografi-par, uten at noe feiler. Det skjer hvis indekstabellen er endret etter at
-- områdebufferen ble bygget — nøyaktig det tilfellet ferskhetsvakten ellers fanger,
-- men her ville den ikke sett det, siden fasett-bufferen stempler sitt EGET
-- radantall.
DECLARE @UtenGeoBucket BIGINT = (SELECT COUNT_BIG(*) FROM #vernsig s
    WHERE NOT EXISTS (SELECT 1 FROM #vernbucket b WHERE b.Signatur = s.Signatur));
IF @UtenGeoBucket > 0
BEGIN
    SET @Msg = CONCAT(FORMAT(@UtenGeoBucket, 'N0'), ' observasjoner har en omraademengde ',
                      'som ikke finnes i AreaCountCacheBucketMember. Omraadebufferen er ',
                      'bygget fra andre data enn indekstabellen inneholder naa. Kjoer ',
                      'BuildAreaCountCache.sql paa nytt foerst. Bufferen bygges IKKE.');
    RAISERROR(@Msg, 16, 1);
    RETURN;
END

SELECT s.ObservationId, b.BucketId
INTO #vern FROM #vernsig s JOIN #vernbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #vern(ObservationId);

SET @Msg = CONCAT('  geografi: ', FORMAT((SELECT COUNT(*) FROM #vernbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

RAISERROR('Leser prosjekt-boetter fra medlemstabellen...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

SELECT ObservationId,
       CAST(STRING_AGG(CAST(ProjectOrgId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY ProjectOrgId) AS NVARCHAR(4000)) AS Signatur
INTO #projsig
FROM dbo.ObservationProject GROUP BY ObservationId;

IF EXISTS (SELECT 1 FROM #projsig WHERE LEN(Signatur) >= 3990)
BEGIN
    RAISERROR('Prosjekt-signaturen naermer seg 4000 tegn og kan vaere avkortet. Bufferen bygges IKKE.', 16, 1);
    RETURN;
END

CREATE TABLE #projbucket (
    Signatur NVARCHAR(4000) COLLATE DATABASE_DEFAULT,
    BucketId INT);

INSERT #projbucket (Signatur, BucketId)
SELECT CAST(STRING_AGG(CAST(MemberId AS VARCHAR(12)), ',') WITHIN GROUP (ORDER BY MemberId) AS NVARCHAR(4000)), BucketId
FROM dbo.AreaCountCacheBucketMember WHERE DimensionId = 13 GROUP BY BucketId;

-- Samme vakt som for geografi. En observasjon UTEN prosjekt er normalt og får
-- NULL-bøtta, men en observasjon som HAR prosjekt uten at mengden finnes som bøtte er
-- det samme avviket som over.
DECLARE @UtenProjBucket BIGINT = (SELECT COUNT_BIG(*) FROM #projsig s
    WHERE NOT EXISTS (SELECT 1 FROM #projbucket b WHERE b.Signatur = s.Signatur));
IF @UtenProjBucket > 0
BEGIN
    SET @Msg = CONCAT(FORMAT(@UtenProjBucket, 'N0'), ' observasjoner har en prosjektmengde ',
                      'som ikke finnes i AreaCountCacheBucketMember. Kjoer ',
                      'BuildAreaCountCache.sql paa nytt foerst. Bufferen bygges IKKE.');
    RAISERROR(@Msg, 16, 1);
    RETURN;
END

SELECT s.ObservationId, b.BucketId
INTO #proj FROM #projsig s JOIN #projbucket b ON b.Signatur = s.Signatur;
CREATE CLUSTERED INDEX ix ON #proj(ObservationId);

SET @Msg = CONCAT('  prosjekt: ', FORMAT((SELECT COUNT(*) FROM #projbucket), 'N0'),
                  ' boetter, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Én rad per observasjon
--
-- MAX() ER IKKE EN TILNÆRMING
-- Kolonnene som leses her er denormaliserte observasjonsattributter og har samme
-- verdi på hver av observasjonens rader. MAX over gruppen er altså verdien, ikke en
-- av flere. Det gjelder ikke EntityTypeId/EntityId, som nettopp er det som varierer —
-- og de leses ikke herfra, men gjennom #vern.
--
-- #obs OPPRETTES UTENFOR DEN DYNAMISKE SETNINGEN
-- En #temp-tabell laget med SELECT INTO inne i sp_executesql lever bare så lenge den
-- setningen gjør. Tabellen må derfor finnes her, og den dynamiske INSERT-en fyller
-- den. Kolonnenavnene kommer fra #dim, så en dimensjon som ikke har en kolonne her
-- feiler høylytt på INSERT-en framfor å mangle stille.
--
-- ObservationId er IKKE med: etter dedupliseringen er hver rad én observasjon, og
-- kolonnen ville bare vært ~240 MB i columnstore for ingenting.
-- ---------------------------------------------------------------------------
CREATE TABLE #obs (
    Regstatus       INT NOT NULL,
    Atferd          INT NOT NULL,
    Funntype        INT NOT NULL,
    Kategori        INT NOT NULL,
    Institusjon     INT NOT NULL,
    Taksongruppe    INT NOT NULL,
    Takson          INT NOT NULL,
    Datasett        INT NOT NULL,
    Periode         INT NOT NULL,
    Koordpresisjon  INT NOT NULL,
    Geografi        INT NOT NULL,
    Prosjekt        INT NOT NULL,
    Mask            TINYINT NOT NULL);

RAISERROR('Bygger en rad per observasjon...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

DECLARE @Kolonner NVARCHAR(MAX) = (
    SELECT STRING_AGG(CAST(QUOTENAME(Navn) AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY Id)
    FROM #dim);

DECLARE @Verdier NVARCHAR(MAX) = (
    SELECT STRING_AGG(CAST(N'MAX(' + Uttrykk + N')' AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY Id)
    FROM #dim);

SET @Sql = N'
INSERT #obs (' + @Kolonner + N', Mask)
SELECT ' + @Verdier + N', MAX(' + @MaskUttrykk + N')
FROM dbo.ObservationEntityIndex i
LEFT JOIN #vern v ON v.ObservationId = i.ObservationId
LEFT JOIN #proj p ON p.ObservationId = i.ObservationId
GROUP BY i.ObservationId
OPTION (RECOMPILE);';
EXEC sp_executesql @Sql;

SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('  ', FORMAT(@Rows, 'N0'), ' observasjoner, ',
                  DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- Columnstore og ikke heap: målt 4,3 s for nivå 1 og 7,7 s for ett nivå-2-par fra
-- heap, altså ~9 minutter for 66 par. Indeksen koster 99 s å bygge og tar det ned til
-- under ett minutt til sammen.
RAISERROR('Bygger columnstore paa #obs...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();
CREATE CLUSTERED COLUMNSTORE INDEX cci ON #obs;
SET @Msg = CONCAT('  ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Nivå 0 — totalen per maske
--
-- 32 rader. Finnes for bool-fasettene uten andre aktive filtre; de grupperes ikke på
-- noen dimensjon, så det er ingen bøttedimensjon å lese summen fra. Summen over alle
-- maskene er totalen uten bool-filter.
-- ---------------------------------------------------------------------------
RAISERROR('--- Nivaa 0 ---', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

INSERT dbo.FacetCountCacheLevel0 (Mask, ObservationCount)
SELECT o.Mask, COUNT_BIG(*) FROM #obs o GROUP BY o.Mask;

SET @Msg = CONCAT('  ', FORMAT(@@ROWCOUNT, 'N0'), ' rader, ',
                  DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Nivå 1
-- ---------------------------------------------------------------------------
DECLARE @Id TINYINT, @Navn VARCHAR(40), @Kol NVARCHAR(60);

RAISERROR('--- Nivaa 1 (12 dimensjoner) ---', 0, 1) WITH NOWAIT;
DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT Id, Navn FROM #dim ORDER BY Id;
OPEN c1; FETCH NEXT FROM c1 INTO @Id, @Navn;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @T = SYSUTCDATETIME();
    SET @Kol = N'o.' + QUOTENAME(@Navn);

    SET @Sql = N'
INSERT dbo.FacetCountCacheLevel1 (DimensionId, BucketId, Mask, ObservationCount)
SELECT ' + CAST(@Id AS VARCHAR(3)) + N', ' + @Kol + N', o.Mask, COUNT_BIG(*)
FROM #obs o
WHERE ' + @Kol + N' <> -2147483648
GROUP BY ' + @Kol + N', o.Mask
OPTION (RECOMPILE);';
    EXEC sp_executesql @Sql;
    SET @Rows = @@ROWCOUNT;

    SET @Msg = CONCAT('  ', @Navn, ': ', FORMAT(@Rows, 'N0'), ' rader, ',
                      DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    FETCH NEXT FROM c1 INTO @Id, @Navn;
END
CLOSE c1; DEALLOCATE c1;

-- ---------------------------------------------------------------------------
-- Nivå 2
-- ---------------------------------------------------------------------------
RAISERROR('--- Nivaa 2 (66 par) ---', 0, 1) WITH NOWAIT;

SELECT a.Id AS IdA, b.Id AS IdB, a.Navn AS NavnA, b.Navn AS NavnB,
       CAST(ROW_NUMBER() OVER (ORDER BY a.Id, b.Id) AS TINYINT) AS PairId
INTO #par
FROM #dim a JOIN #dim b ON b.Id > a.Id;

DECLARE @NavnA VARCHAR(40), @NavnB VARCHAR(40), @KolA NVARCHAR(60), @KolB NVARCHAR(60),
        @PairId TINYINT;

DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR
    SELECT NavnA, NavnB, PairId FROM #par ORDER BY PairId;
OPEN c2; FETCH NEXT FROM c2 INTO @NavnA, @NavnB, @PairId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @T = SYSUTCDATETIME();
    SET @KolA = N'o.' + QUOTENAME(@NavnA);
    SET @KolB = N'o.' + QUOTENAME(@NavnB);

    SET @Sql = N'
INSERT dbo.FacetCountCacheLevel2 (DimensionPairId, BucketA, BucketB, Mask, ObservationCount)
SELECT ' + CAST(@PairId AS VARCHAR(3)) + N', ' + @KolA + N', ' + @KolB + N', o.Mask, COUNT_BIG(*)
FROM #obs o
WHERE ' + @KolA + N' <> -2147483648 AND ' + @KolB + N' <> -2147483648
GROUP BY ' + @KolA + N', ' + @KolB + N', o.Mask
OPTION (RECOMPILE);';
    EXEC sp_executesql @Sql;
    SET @Rows = @@ROWCOUNT;

    SET @Msg = CONCAT('  [', @PairId, '/66] ', @NavnA, ' x ', @NavnB, ': ',
                      FORMAT(@Rows, 'N0'), ' rader, ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's',
                      ' | totalt ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    FETCH NEXT FROM c2 INTO @NavnA, @NavnB, @PairId;
END
CLOSE c2; DEALLOCATE c2;

-- ---------------------------------------------------------------------------
-- Ferdig
-- ---------------------------------------------------------------------------
-- FULLSCAN, ikke standard utvalg. Tabellene er nettopp TRUNCATE-t og fylt, så
-- statistikken bygges fra bunnen uansett — forskjellen er bare om den bygges av hele
-- tabellen eller en liten prøve. Utvalgsstatistikk på en tabell i denne
-- størrelsesordenen har før gjort LocationCount sju ganger tregere, og en tellebuffer
-- som er treg å lese er hele poenget borte.
UPDATE STATISTICS dbo.FacetCountCacheLevel0 WITH FULLSCAN;
UPDATE STATISTICS dbo.FacetCountCacheLevel1 WITH FULLSCAN;
UPDATE STATISTICS dbo.FacetCountCacheLevel2 WITH FULLSCAN;

UPDATE dbo.FacetCountCacheState
SET Status = 'Ready',
    BuiltAt = SYSUTCDATETIME(),
    SourceRows = @SourceRows,
    DurationSeconds = DATEDIFF(SECOND, @Start, SYSUTCDATETIME())
WHERE Id = 1;

SELECT 'Nivaa 0' AS Tabell, FORMAT(COUNT_BIG(*), 'N0') AS Rader FROM dbo.FacetCountCacheLevel0
UNION ALL SELECT 'Nivaa 1', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.FacetCountCacheLevel1
UNION ALL SELECT 'Nivaa 2', FORMAT(COUNT_BIG(*), 'N0') FROM dbo.FacetCountCacheLevel2;

SET @Msg = CONCAT('Ferdig paa ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ============================================================================
-- Backfill av dbo.ObservationTaxonHierarchy
-- Kjøres manuelt i SSMS etter at migrasjon 20260702113806 har opprettet
-- tabellen og indeksene.
--
-- Fyller én rad per observasjon med taksonhierarkiet utledet fra
-- Taxon.TaxonIdHiarchy, én kolonne per rang (KingdomTaxonId ... NotSetTaxonId).
--
-- HVORFOR EGET SKRIPT:
-- Datafyllingen lå opprinnelig inne i migrasjonen som én stor INSERT. Den
-- kjørte på ~7 minutter lokalt, men over 30 minutter på Azure SQL og traff da
-- både CommandTimeout(1800) og oppstartsgrensen til App Service. Årsaken er
-- ikke CPU: Azure SQL har hardt tak på loggskriving per servicenivå, og hele
-- migrasjonen lå i én transaksjon, så loggen kunne aldri avkortes underveis.
-- En feil ville i tillegg gitt en like lang rollback.
--
-- Her kjører hver batch som sin egen implisitte transaksjon, så loggen
-- avkortes fortløpende og en avbrutt kjøring mister bare siste batch.
--
-- Skriptet kan trygt kjøres flere ganger — allerede fylte observasjoner
-- hoppes over, og en avbrutt kjøring fortsetter der den slapp.
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.ObservationTaxonHierarchy') IS NULL
BEGIN
    RAISERROR('AVBRUTT: dbo.ObservationTaxonHierarchy finnes ikke. Har migrasjonene kjoert?', 16, 1) WITH NOWAIT;
    RETURN;
END

DECLARE @Msg NVARCHAR(500);
DECLARE @Sql NVARCHAR(MAX);

-- ----------------------------------------------------------------------------
-- 1) Deaktiver de nonclustered indeksene
--
-- Tabellen har 25 filtrerte rangindekser. Holdes de aktive under innlastingen,
-- vedlikeholdes samtlige rad for rad — det er den klart dyreste delen av
-- jobben, og det meste av loggvolumet.
--
-- Kun NONCLUSTERED deaktiveres. Deaktiveres klyngeindeksen (PK), blir hele
-- tabellen utilgjengelig og INSERT-en under feiler.
--
-- Indeksene hentes fra sys.indexes i stedet for en fast liste, slik at
-- skriptet følger tabellen om rangindekser kommer til eller forsvinner.
-- ----------------------------------------------------------------------------
SET @Sql = N'';

SELECT @Sql = @Sql + N'ALTER INDEX ' + QUOTENAME(i.name)
                   + N' ON dbo.ObservationTaxonHierarchy DISABLE;' + CHAR(10)
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.ObservationTaxonHierarchy')
  AND i.type_desc = 'NONCLUSTERED'
  AND i.is_disabled = 0;

IF @Sql <> N''
BEGIN
    RAISERROR('Deaktiverer nonclustered indekser...', 0, 1) WITH NOWAIT;
    EXEC sp_executesql @Sql;
    RAISERROR('Indekser deaktivert.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Ingen aktive nonclustered indekser aa deaktivere (allerede deaktivert?).', 0, 1) WITH NOWAIT;

RAISERROR('---', 0, 1) WITH NOWAIT;

-- ----------------------------------------------------------------------------
-- 2) Fyll tabellen batchvis
--
-- Batchene går på Observation.Id. Hierarkitabellen er clustered på
-- ObservationId, så radene skrives da tilnærmet sekvensielt inn i stigende
-- sider i stedet for spredt utover.
-- ----------------------------------------------------------------------------
DECLARE @BatchSize INT = 500000;
DECLARE @MinId INT;
DECLARE @MaxId INT;
DECLARE @CurrentId INT;
DECLARE @BatchEnd INT;
DECLARE @RowsInserted INT;
DECLARE @TotalInserted BIGINT = 0;
DECLARE @StartTime DATETIME2 = SYSUTCDATETIME();

SELECT @MinId = MIN(Id), @MaxId = MAX(Id)
FROM dbo.Observation
WHERE IsDeleted = 0;

IF @MinId IS NULL
BEGIN
    RAISERROR('AVBRUTT: ingen rader i dbo.Observation.', 16, 1) WITH NOWAIT;
    RETURN;
END

SET @CurrentId = @MinId;

SET @Msg = CONCAT('ObservationId-range: ', FORMAT(@MinId, 'N0'), ' - ', FORMAT(@MaxId, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Estimerte batcher: ', CEILING(CAST(@MaxId - @MinId AS FLOAT) / @BatchSize));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
RAISERROR('---', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    INSERT INTO dbo.ObservationTaxonHierarchy (
        ObservationId, KingdomTaxonId, SubkingdomTaxonId, PhylumTaxonId, SubphylumTaxonId,
        SuperclassTaxonId, ClassTaxonId, SubclassTaxonId, InfraclassTaxonId, CohortTaxonId,
        SuperorderTaxonId, OrderTaxonId, SuborderTaxonId, InfraorderTaxonId, SuperfamilyTaxonId,
        FamilyTaxonId, SubfamilyTaxonId, TribeTaxonId, SubtribeTaxonId, GenusTaxonId,
        SubgenusTaxonId, SectionTaxonId, SpeciesTaxonId, SubspeciesTaxonId, VarietyTaxonId,
        FormTaxonId, NotSetTaxonId)
    SELECT
        o.Id,
        MAX(CASE WHEN ancestor.TaxonRankId = 1 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 2 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 3 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 4 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 5 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 6 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 7 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 8 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 9 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 10 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 11 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 12 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 13 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 14 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 15 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 16 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 17 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 18 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 19 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 20 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 21 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 22 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 23 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 24 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 25 THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 26 THEN ancestor.Id END)
    FROM dbo.Observation o
    JOIN dbo.Taxon t ON o.TaxonId = t.Id
    CROSS APPLY STRING_SPLIT(t.TaxonIdHiarchy, ',') AS s
    JOIN dbo.Taxon ancestor ON ancestor.Id = TRY_CAST(s.value AS INT)
    WHERE o.IsDeleted = 0
      AND o.Id >= @CurrentId
      AND o.Id <= @BatchEnd
      -- Gjør skriptet resumerbart: hopp over observasjoner som alt er fylt.
      AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTaxonHierarchy h
                      WHERE h.ObservationId = o.Id)
    GROUP BY o.Id;

    SET @RowsInserted = @@ROWCOUNT;
    SET @TotalInserted = @TotalInserted + @RowsInserted;

    SET @Msg = CONCAT(
        FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | ',
        'Range: ', FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'), ' | ',
        'Satt inn: ', FORMAT(@RowsInserted, 'N0'), ' rader | ',
        'Totalt: ', FORMAT(@TotalInserted, 'N0'), ' | ',
        'Tid: ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), 's'
    );
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

RAISERROR('---', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Innlasting ferdig! Totalt satt inn: ', FORMAT(@TotalInserted, 'N0'), ' rader paa ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), ' sekunder.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ----------------------------------------------------------------------------
-- 3) Gjenoppbygg indeksene
--
-- En deaktivert indeks kan ikke reaktiveres — REBUILD er eneste vei tilbake.
--
-- DATA_COMPRESSION = PAGE settes eksplisitt. En deaktivert indeks har ingen
-- allokerte sider, så komprimeringsinnstillingen er ikke til å stole på her;
-- uten dette risikerer man ukomprimerte indekser, og tabellen går da fra
-- ~10 GB til ~21 GB.
--
-- Bygges én om gangen med MAXDOP = 4 for å holde loggpresset nede. Regn
-- 10-20 minutter for alle 25 til sammen.
--
-- MERK: Feiler eller avbrytes skriptet før dette punktet, står indeksene
-- fortsatt DISABLED og spørringer mot hierarkitabellen blir svært trege.
-- Kjør skriptet på nytt — det fortsetter innlastingen og bygger dem opp.
-- ----------------------------------------------------------------------------
DECLARE @IndexName SYSNAME;

DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT i.name
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('dbo.ObservationTaxonHierarchy')
      AND i.type_desc = 'NONCLUSTERED'
      AND i.is_disabled = 1
    ORDER BY i.name;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | Gjenoppbygger ', @IndexName, '...');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @Sql = N'ALTER INDEX ' + QUOTENAME(@IndexName)
             + N' ON dbo.ObservationTaxonHierarchy REBUILD WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);';
    EXEC sp_executesql @Sql;

    FETCH NEXT FROM index_cursor INTO @IndexName;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

SET @Msg = CONCAT('Ferdig! Total tid: ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), ' sekunder.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ----------------------------------------------------------------------------
-- Kontroll
-- ----------------------------------------------------------------------------

-- Dekning: antall rader her skal ligge tett opp mot antall ikke-slettede
-- observasjoner. Differansen er observasjoner uten takson eller uten hierarki.
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.ObservationTaxonHierarchy)       AS RaderIHierarkitabellen,
    (SELECT COUNT_BIG(*) FROM dbo.Observation WHERE IsDeleted = 0) AS IkkeSletteteObservasjoner;

-- Ingen indekser skal staa igjen som deaktiverte, og alle skal vaere PAGE-komprimert.
SELECT
    i.name                  AS Indeks,
    i.type_desc             AS Type,
    i.is_disabled           AS ErDeaktivert,
    p.data_compression_desc AS Komprimering,
    CAST(SUM(au.total_pages) * 8.0 / 1024 / 1024 AS DECIMAL(10,2)) AS StoerrelseGB
FROM sys.indexes i
JOIN sys.partitions p
  ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units au
  ON au.container_id = p.partition_id
WHERE i.object_id = OBJECT_ID('dbo.ObservationTaxonHierarchy')
GROUP BY i.name, i.type_desc, i.is_disabled, p.data_compression_desc
ORDER BY i.type_desc, i.name;

-- ============================================================================
-- BackfillAll — samlet datafylling for ObservationEntityIndex,
-- ObservationTaxonHierarchy og CompleteFilter-kolonnene.
--
-- IDENTISK MED MIGRASJONEN BackfillAll. Endres den ene, må den andre endres
-- med. Migrasjonen er det som kjører i pipeline; dette skriptet finnes for
-- manuelle kjøringer og reparasjon.
--
-- ERSTATTER fire tidligere skript som er slettet (se git-historikken):
--   BackfillObservationTaxonHierarchy, BackfillObservationEntityIndex,
--   BackfillTaxonHierarchyColumns, BackfillCompleteFilter.
-- De fire kjørte i en rekkefølge ingen håndhevet, deaktiverte og bygde opp
-- igjen de samme indeksene hver for seg, og etterlot ingen spor av hva som var
-- gjort. Her er rekkefølgen kodet, indeksene håndteres én gang, og
-- BackfillProgress viser hvor langt man er kommet.
--
-- KONTRAKT: etter en vellykket kjøring er dataene korrekte, uansett hva som
-- måtte ha feilet tidligere. Skriptet fyller ikke bare kolonner — det setter
-- også inn rader som mangler i ObservationEntityIndex fordi migrasjon
-- 20260608073333 stoppet halvveis.
--
-- KAN AVBRYTES OG STARTES PÅ NYTT. Hver seksjon fører sitt eget vannmerke i
-- dbo.BackfillProgress, så en ny kjøring hopper rett til der den forrige
-- stoppet i stedet for å skanne seg gjennom ferdig arbeid på nytt. Det er
-- forskjellen på å konvergere og å bruke hele tidsbudsjettet på å bekrefte
-- gammelt arbeid.
--
-- VIKTIG: må kjøre uten omsluttende transaksjon. Med alt i én transaksjon
-- ruller en timeout tilbake hele kjøringen, og da konvergerer den aldri
-- uansett hvor mange forsøk man gir den.
--
-- EIER COLUMNSTORE-INDEKSEN: skriptet slipper IX_OEI_Columnstore øverst og
-- oppretter den nederst med full kolonneliste, inkludert CompleteFilter-
-- kolonnene. Det er derfor det ikke finnes noen egen migrasjon som utvider den —
-- da ville indeksen blitt bygget to ganger à 10-30 minutter.
--
-- KJØRETID: timer, ikke minutter, i et tomt produksjonsmiljø.
-- ============================================================================

SET NOCOUNT ON;

DECLARE @Msg NVARCHAR(500);
DECLARE @Sql NVARCHAR(MAX);
DECLARE @BatchSize INT = 500000;
DECLARE @MinId INT, @MaxId INT, @CurrentId INT, @BatchEnd INT;
DECLARE @Rows INT, @Total BIGINT, @SectionStart DATETIME2;
DECLARE @RunStart DATETIME2 = SYSUTCDATETIME();

-- ---------------------------------------------------------------------------
-- ALLE BATCH-SETNINGER HAR OPTION (RECOMPILE). Det er ingen mikrooptimalisering.
--
-- @CurrentId og @BatchEnd er lokale variabler. SQL Server kan ikke lese verdien
-- deres ved kompilering og gjetter i stedet en fast andel av tabellen — rundt
-- 9 % for et tosidig intervall — uansett hvor smalt intervallet faktisk er.
--
-- Seksjon A–E1 tåler det: de har alle intervallpredikatet på en klyngeindeks
-- (Observation.Id eller ObservationEntityIndex.ObservationId), så en feilestimert
-- plan blir likevel en seek, og arbeidet avgrenses av de faktiske radene.
--
-- E2 er den eneste seksjonen som leser rett fra OrganizationRelation med
-- intervallet på ObservationId — en NONCLUSTERED nøkkel; tabellen er klynget på
-- Id. Uten RECOMPILE ble ~300 000 rader estimert til flere millioner, og både
-- DISTINCT-en og NOT EXISTS-en fikk minnetildeling etter estimatet.
-- Minnetildelingen er fast per plan, så kostnaden ble den samme for hver eneste
-- batch — uavhengig av hvor mye som faktisk ble skrevet.
--
-- Målt i testmiljøet før denne endringen: ~190 sekunder per batch i E2, flatt
-- over to kjøringer på til sammen 12 timer, mens ObservationDataset vokste fra
-- nær tom til flere millioner rader. En kostnad som ikke følger datamengden er
-- en kostnad som følger estimatet.
--
-- Rekompilering koster millisekunder per batch. Batchene er minutter.
-- ---------------------------------------------------------------------------

-- ---------------------------------------------------------------------------
-- Preflight — alt skjema må være på plass før datafyllingen gir mening.
-- Feiler heller her med en tydelig melding enn å hoppe stille over seksjoner.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.ObservationEntityIndex') IS NULL
    OR OBJECT_ID('dbo.ObservationTaxonHierarchy') IS NULL
    OR OBJECT_ID('dbo.ObservationDataset') IS NULL
    OR COL_LENGTH('dbo.Observation', 'InstitutionOrgId') IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'BehaviorId') IS NULL
BEGIN
    RAISERROR('AVBRUTT: skjemaet er ikke komplett. Kjoer alle migrasjoner foerst.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- OrganizationRelation er kilden for seksjon E1 og E2, og slippes av
-- oppryddingsmigrasjonen. Sjekkes her slik at skriptet feiler umiddelbart med en
-- forstaaelig melding, i stedet for «Invalid object name» dypt inne i en kjøring.
IF OBJECT_ID('dbo.OrganizationRelation') IS NULL
BEGIN
    RAISERROR('AVBRUTT: dbo.OrganizationRelation finnes ikke. Oppryddingsmigrasjonen har alt kjoert, og dette skriptet kan ikke lenger brukes til reparasjon.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- Fremdriftstabell. Vannmerket er det som gjør gjentatte forsøk billige.
IF OBJECT_ID('dbo.BackfillProgress') IS NULL
BEGIN
    CREATE TABLE dbo.BackfillProgress (
        Section         NVARCHAR(64)  NOT NULL PRIMARY KEY,
        LastCompletedId INT           NOT NULL,
        UpdatedAt       DATETIME2     NOT NULL CONSTRAINT DF_BackfillProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    RAISERROR('Opprettet dbo.BackfillProgress.', 0, 1) WITH NOWAIT;
END

-- ID-intervallet dekker ALLE observasjoner, også slettede. Intervallet er kun en
-- løkkegrense; hver seksjon har sine egne per-rad-filtre (seksjon B fyller f.eks.
-- bare hierarki for IsDeleted = 0).
--
-- Tidligere ble intervallet regnet ut med WHERE IsDeleted = 0. Da falt slettede
-- observasjoner utenfor ytterpunktene ut av backfillen, samtidig som
-- verifiseringen og ALTER COLUMN nedstrøms teller alle rader — en asymmetri som
-- gjorde verifiseringen umulig å tilfredsstille.
SELECT @MinId = MIN(Id), @MaxId = MAX(Id) FROM dbo.Observation;

-- Tom Observation-tabell er en gyldig tilstand, ikke en feil: det er slik en
-- fersk lokal database ser ut.
--
-- Vi returnerer IKKE her. Da ville columnstore-indeksen nederst aldri blitt
-- opprettet, og en fersk database endt opp uten den. I stedet settes et tomt
-- ID-intervall, slik at hver WHILE-løkke hopper over seg selv mens resten av
-- skriptet — indeksene og verifiseringen — kjører som normalt.
IF @MinId IS NULL
BEGIN
    RAISERROR('Ingen rader i dbo.Observation - hopper over datafyllingen.', 0, 1) WITH NOWAIT;
    SET @MinId = 0;
    SET @MaxId = -1;
END

SET @Msg = CONCAT('ObservationId-range: ', FORMAT(@MinId, 'N0'), ' - ', FORMAT(@MaxId, 'N0'),
                  ' | Batchstoerrelse: ', FORMAT(@BatchSize, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ---------------------------------------------------------------------------
-- Deaktiver rowstore-indeksene på begge de store tabellene — én gang for hele
-- kjøringen, ikke én gang per seksjon slik de fire gamle skriptene gjorde.
--
-- Kun type = 2 (nonclustered rowstore). Klyngeindeksen (type 1) må ikke røres —
-- deaktiveres den, blir tabellen utilgjengelig. Columnstore håndteres for seg
-- rett under.
--
-- Feiler kjøringen etter dette punktet står indeksene igjen som DISABLED og
-- spørringer blir svært trege. Kjør skriptet på nytt; det fortsetter der det
-- slapp og bygger dem opp til slutt.
--
-- GJØRES BARE HVIS DET FAKTISK ER ARBEID IGJEN. Uten denne sjekken deaktiverte og
-- gjenoppbygde hver kjøring ~28 rowstore-indekser og hele columnstore-indeksen —
-- 10-30 minutter — selv når alle seksjoner var ferdige og ingenting ble skrevet.
-- Kombinert med en feilende verifisering ga det en evig løkke av full-rebuilds.
-- ---------------------------------------------------------------------------
DECLARE @HarArbeid BIT = 0;

IF @MaxId >= @MinId AND EXISTS (
    SELECT 1 FROM (VALUES
        ('A_EntityIndexRows'), ('B_TaxonHierarchy'), ('C_EntityIndexFilterColumns'),
        ('D_EntityIndexTaxonRanks'), ('E1_ObservationOrgColumns'),
        ('E2_ObservationDataset'), ('E3_EntityIndexOrgColumns'),
        ('E4_EntityIndexBehavior')) AS s(Section)
    WHERE ISNULL((SELECT p.LastCompletedId FROM dbo.BackfillProgress p
                  WHERE p.Section = s.Section), @MinId - 1) < @MaxId)
    SET @HarArbeid = 1;

IF @HarArbeid = 0
    RAISERROR('Alle seksjoner er allerede fullfoert - hopper over indekshaandtering.', 0, 1) WITH NOWAIT;

SET @Sql = N'';

IF @HarArbeid = 1
BEGIN
    SELECT @Sql = @Sql + N'ALTER INDEX ' + QUOTENAME(i.name) + N' ON '
                       + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.'
                       + QUOTENAME(OBJECT_NAME(i.object_id)) + N' DISABLE;' + CHAR(10)
    FROM sys.indexes i
    WHERE i.object_id IN (OBJECT_ID('dbo.ObservationEntityIndex'),
                          OBJECT_ID('dbo.ObservationTaxonHierarchy'),
                          OBJECT_ID('dbo.ObservationDataset'))
      AND i.type = 2
      AND i.is_disabled = 0;

    IF @Sql <> N''
    BEGIN
        RAISERROR('Deaktiverer rowstore-indekser...', 0, 1) WITH NOWAIT;
        EXEC sp_executesql @Sql;
    END
    ELSE
        RAISERROR('Ingen aktive rowstore-indekser aa deaktivere.', 0, 1) WITH NOWAIT;
END

-- ---------------------------------------------------------------------------
-- Columnstore SLIPPES, den deaktiveres ikke.
--
-- CompleteFilter legger til tre kolonner som skal inn i indeksen, og en
-- columnstore-indeks kan ikke utvides med ALTER — den må opprettes på nytt.
-- Deaktiverte vi den her og bygde den opp igjen til slutt, ville vi fått den
-- gamle kolonnelisten, og en egen migrasjon måtte deretter slippe og bygge den
-- en gang til. Det ble to fulle bygg à 10-30 minutter.
--
-- Ved å slippe den her og opprette den med full kolonneliste nederst, bygges
-- den nøyaktig én gang — og da med data allerede på plass.
--
-- Konsekvens: indeksen finnes ikke mens backfillen pågår, og områdetellinger er
-- ~10x tregere i det vinduet. Det var den også før, da den lå deaktivert.
--
-- Slippes bare når det er arbeid igjen. Ellers kostet hver kjøring et fullt
-- gjenoppbygg over 192M rader uten å skrive en eneste rad.
-- ---------------------------------------------------------------------------
IF @HarArbeid = 1 AND EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Slipper IX_OEI_Columnstore (opprettes paa nytt til slutt)...', 0, 1) WITH NOWAIT;
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;
END


-- ===========================================================================
-- SEKSJON A: rader i ObservationEntityIndex
--
-- Lå opprinnelig som to INSERT-setninger i migrasjon 20260608073333, i én
-- transaksjon. Her batchvis og idempotent, slik at et miljø der migrasjonen
-- stoppet halvveis blir reparert i stedet for å måtte bygges på nytt.
--
-- Radene får TaxonGroupId = 0 fra defaultverdien, som er nøyaktig markøren
-- seksjon C leter etter. Nye rader plukkes derfor opp automatisk.
-- ===========================================================================
RAISERROR('=== SEKSJON A: ObservationEntityIndex-rader ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'A_EntityIndexRows'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Seksjon A allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    -- Områder: Observation -> Location -> LocationAreas -> Area.
    -- AreaTypeId speiler ObservationIndexEntityType 1-4 direkte.
    -- Fid konverteres til int: RestrictedArea (3) har prefikset "Naturbase VV",
    -- historiske fylkes-Fid-er har understrek ("15_2017").
    INSERT INTO dbo.ObservationEntityIndex (ObservationId, EntityTypeId, EntityId)
    SELECT DISTINCT o.Id, a.AreaTypeId,
        CASE WHEN a.AreaTypeId = 3 THEN CAST(REPLACE(a.Fid, 'Naturbase VV', '') AS INT)
             ELSE CAST(REPLACE(a.Fid, '_', '') AS INT) END
    FROM dbo.Observation o
    JOIN dbo.Location l       ON l.Id = o.LocationId
    JOIN dbo.LocationAreas la ON la.LocationId = l.Id
    JOIN dbo.Area a           ON a.Id = la.AreaId
    WHERE a.IsCurrent = 1
      AND o.LocationId IS NOT NULL
      AND o.Id >= @CurrentId AND o.Id <= @BatchEnd
      AND NOT EXISTS (
            SELECT 1 FROM dbo.ObservationEntityIndex x
            WHERE x.ObservationId = o.Id
              AND x.EntityTypeId = a.AreaTypeId
              AND x.EntityId = CASE WHEN a.AreaTypeId = 3
                                    THEN CAST(REPLACE(a.Fid, 'Naturbase VV', '') AS INT)
                                    ELSE CAST(REPLACE(a.Fid, '_', '') AS INT) END)
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    -- INSTITUSJONSRADER (EntityTypeId = 101) SETTES BEVISST IKKE INN.
    --
    -- Migrasjon 20260608073333 satte dem inn, og de finnes derfor i eksisterende
    -- miljøer — men CompleteFilter erstatter dem med kolonnen InstitutionOrgId, og
    -- RemoveInstitutionIndexRows sletter alle 61 052 216 like etterpå.
    --
    -- Å gjenskape dem her ville betydd: 61M innsettinger, fire kolonneoppdateringer
    -- over de samme radene i seksjon C/D/E3/E4, et columnstore-bygg over 192M i
    -- stedet for 131M rader, og deretter 61M slettinger — alt for å ende i samme
    -- tilstand som ved å la være. Ingen spørring leser dem lenger.

    -- Vannmerket settes etter batchen. Uten transaksjon rundt er batchen alt
    -- committet, så vannmerket er sant i det øyeblikket det skrives.
    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'A_EntityIndexRows' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | A | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Satt inn: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END


-- ===========================================================================
-- SEKSJON B: ObservationTaxonHierarchy
--
-- Én rad per ikke-slettet observasjon, én kolonne per rang, utledet fra
-- Taxon.TaxonIdHiarchy.
-- ===========================================================================
RAISERROR('=== SEKSJON B: ObservationTaxonHierarchy ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'B_TaxonHierarchy'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Seksjon B allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

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
        MAX(CASE WHEN ancestor.TaxonRankId = 1  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 2  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 3  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 4  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 5  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 6  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 7  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 8  THEN ancestor.Id END),
        MAX(CASE WHEN ancestor.TaxonRankId = 9  THEN ancestor.Id END),
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
      AND o.Id >= @CurrentId AND o.Id <= @BatchEnd
      AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTaxonHierarchy h
                      WHERE h.ObservationId = o.Id)
    GROUP BY o.Id
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'B_TaxonHierarchy' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | B | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Satt inn: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END


-- ===========================================================================
-- SEKSJON C: denormaliserte filterkolonner på ObservationEntityIndex
--
-- Markør for uoppdatert rad er TaxonGroupId = 0 (defaultverdien fra
-- migrasjon 20260814125543).
--
-- RegistrationStatusId: 1 = Funn, 2 = Ikke funnet (TagId 5 / Absent),
-- 3 = Ikke gjenfunnet (TagId 6 / NotRecovered).
-- ===========================================================================
RAISERROR('=== SEKSJON C: OEI denormaliserte filterkolonner ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'C_EntityIndexFilterColumns'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Seksjon C allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    UPDATE idx
    SET idx.TaxonGroupId               = o.TaxonGroupId,
        idx.CategoryId                 = o.CategoryId,
        idx.BasisOfRecordId            = o.BasisOfRecordId,
        idx.CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters,
        idx.DateTimeCollected          = o.DateTimeCollected,
        idx.HasMediaFiles = CASE WHEN EXISTS (
            SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id) THEN 1 ELSE 0 END,
        idx.RegistrationStatusId = CASE
            WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 6) THEN 3
            WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 5) THEN 2
            ELSE 1 END
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
      AND idx.TaxonGroupId = 0
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'C_EntityIndexFilterColumns' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | C | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END


-- ===========================================================================
-- SEKSJON D: taksonrangkolonner på ObservationEntityIndex
-- Krever at seksjon B er ferdig — leser fra ObservationTaxonHierarchy.
-- ===========================================================================
RAISERROR('=== SEKSJON D: OEI taksonrangkolonner ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'D_EntityIndexTaxonRanks'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Seksjon D allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    UPDATE idx
    SET idx.SpeciesTaxonId = h.SpeciesTaxonId,
        idx.GenusTaxonId   = h.GenusTaxonId,
        idx.FamilyTaxonId  = h.FamilyTaxonId,
        idx.OrderTaxonId   = h.OrderTaxonId
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
      -- Predikatet må være OPPFYLLBART: det treffer bare rader der kilden har en
      -- verdi indeksen mangler. Etter oppdateringen matcher raden ikke lenger.
      --
      -- Den forrige varianten var «idx.OrderTaxonId IS NULL AND (h.Species IS NOT
      -- NULL OR ...)». For en observasjon med art men UTEN ordensrang skrev D
      -- NULL til OrderTaxonId, og raden matchet fortsatt etterpå. Som
      -- oppdateringsfilter var det bare bortkastet arbeid, men verifiseringen
      -- nederst brukte samme predikat og kunne dermed aldri nå null.
      AND (   (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
           OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
           OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
           OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL))
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'D_EntityIndexTaxonRanks' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | D | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END


-- ===========================================================================
-- SEKSJON E: CompleteFilter
--
-- E1 Observation.InstitutionOrgId + CollectionOrgId  (begge verifisert 1:1)
-- E2 ObservationDataset                              (datasett er IKKE 1:1)
-- E3 OEI.InstitutionOrgId + CollectionOrgId          (leser E1)
-- E4 OEI.BehaviorId                                  (verifisert 1:1, tinyint)
--
-- E3 avhenger av E1, så rekkefølgen er ikke valgfri.
-- ===========================================================================
-- ---------------------------------------------------------------------------
-- Midlertidig dekkende indeks for E1 og E2.
--
-- Begge leser OrganizationRelation filtrert på ObservationId-intervall og
-- trenger OrganizationId ut. IX_ObservationId inneholder ikke OrganizationId,
-- så hver treffrad koster et key lookup mot klyngeindeksen — rundt 1,1 millioner
-- oppslag per batch mot en tabell med 136M rader. Det er den enkeltposten som
-- gjør E1 og E2 trege.
--
-- Med INCLUDE (OrganizationId) blir begge seksjonene rene dekkede seek.
-- Indeksen er ren engangsinvestering: OrganizationRelation slippes uansett i
-- oppryddingsmigrasjonen.
--
-- Opprettes bare hvis E1 eller E2 faktisk har arbeid igjen, slik at en ny
-- kjøring etter at de er ferdige ikke bygger den på nytt til ingen nytte.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OrgRel_Obs_Org'
                 AND object_id = OBJECT_ID('dbo.OrganizationRelation'))
   AND (ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                WHERE Section = 'E1_ObservationOrgColumns'), @MinId - 1) < @MaxId
     OR ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                WHERE Section = 'E2_ObservationDataset'), @MinId - 1) < @MaxId)
BEGIN
    RAISERROR('Oppretter midlertidig IX_OrgRel_Obs_Org...', 0, 1) WITH NOWAIT;

    CREATE NONCLUSTERED INDEX IX_OrgRel_Obs_Org
    ON dbo.OrganizationRelation (ObservationId)
    INCLUDE (OrganizationId)
    WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);

    RAISERROR('IX_OrgRel_Obs_Org opprettet.', 0, 1) WITH NOWAIT;
END

RAISERROR('=== SEKSJON E1: Observation.InstitutionOrgId + CollectionOrgId ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'E1_ObservationOrgColumns'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    -- MAX(CASE ...) er trygt fordi begge typer er verifisert 1:1 per observasjon.
    UPDATE o
    SET o.InstitutionOrgId = x.InstitutionOrgId,
        o.CollectionOrgId  = x.CollectionOrgId
    FROM dbo.Observation o
    INNER JOIN (
        SELECT r.ObservationId,
               MAX(CASE WHEN g.OrganizationTypeId = 1 THEN r.OrganizationId END) AS InstitutionOrgId,
               MAX(CASE WHEN g.OrganizationTypeId = 2 THEN r.OrganizationId END) AS CollectionOrgId
        FROM dbo.OrganizationRelation r
        INNER JOIN dbo.Organization g ON g.Id = r.OrganizationId
        WHERE r.ObservationId >= @CurrentId AND r.ObservationId <= @BatchEnd
          AND g.OrganizationTypeId IN (1, 2)
        GROUP BY r.ObservationId
    ) x ON x.ObservationId = o.Id
    WHERE o.Id >= @CurrentId AND o.Id <= @BatchEnd
      -- Begge kolonnene sjekkes. Med bare InstitutionOrgId som markør ville en
      -- observasjon med institusjon men uten samling fått markøren brukt opp og
      -- CollectionOrgId stående NULL for godt.
      AND (o.InstitutionOrgId IS NULL OR o.CollectionOrgId IS NULL)
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'E1_ObservationOrgColumns' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | E1 | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

RAISERROR('=== SEKSJON E2: ObservationDataset ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'E2_ObservationDataset'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    INSERT INTO dbo.ObservationDataset (ObservationId, DatasetOrgId)
    SELECT DISTINCT r.ObservationId, r.OrganizationId
    FROM dbo.OrganizationRelation r
    INNER JOIN dbo.Organization g ON g.Id = r.OrganizationId
    WHERE r.ObservationId >= @CurrentId AND r.ObservationId <= @BatchEnd
      AND g.OrganizationTypeId = 3
      AND NOT EXISTS (SELECT 1 FROM dbo.ObservationDataset d
                      WHERE d.ObservationId = r.ObservationId
                        AND d.DatasetOrgId = r.OrganizationId)
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'E2_ObservationDataset' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | E2 | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Satt inn: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

-- E1 og E2 er ferdige — den midlertidige indeksen har gjort sitt.
-- Slippes her i stedet for å bli liggende til oppryddingsmigrasjonen, slik at
-- den ikke koster plass og skrivevedlikehold i mellomtiden.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OrgRel_Obs_Org'
             AND object_id = OBJECT_ID('dbo.OrganizationRelation'))
BEGIN
    RAISERROR('Slipper midlertidig IX_OrgRel_Obs_Org.', 0, 1) WITH NOWAIT;
    DROP INDEX IX_OrgRel_Obs_Org ON dbo.OrganizationRelation;
END

RAISERROR('=== SEKSJON E3: OEI InstitutionOrgId + CollectionOrgId ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'E3_EntityIndexOrgColumns'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    UPDATE idx
    SET idx.InstitutionOrgId = o.InstitutionOrgId,
        idx.CollectionOrgId  = o.CollectionOrgId
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
      -- Samme oppfyllbarhetsregel som seksjon D: treff bare rader der kilden har
      -- en verdi indeksen mangler, slik at raden ikke matcher etterpå.
      AND (   (idx.InstitutionOrgId IS NULL AND o.InstitutionOrgId IS NOT NULL)
           OR (idx.CollectionOrgId  IS NULL AND o.CollectionOrgId  IS NOT NULL))
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'E3_EntityIndexOrgColumns' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | E3 | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

RAISERROR('=== SEKSJON E4: OEI BehaviorId ===', 0, 1) WITH NOWAIT;

-- NULL er en gyldig sluttilstand her: bare ~27 % av observasjonene har atferd.
-- Derfor kan ikke "BehaviorId IS NULL" bety "ikke fylt ut" — joinen mot
-- ObservationBehaviors er det som avgjør om det finnes noe å skrive.
SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'E4_EntityIndexBehavior'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    UPDATE idx
    SET idx.BehaviorId = CAST(b.BehaviorId AS TINYINT)
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.ObservationBehaviors b ON b.ObservationId = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
      AND idx.BehaviorId IS NULL
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'E4_EntityIndexBehavior' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | E4 | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END


-- ---------------------------------------------------------------------------
-- Gjenoppbygg indeksene.
--
-- En deaktivert indeks kan ikke reaktiveres — REBUILD er eneste vei tilbake.
-- Rowstore får DATA_COMPRESSION = PAGE eksplisitt: en deaktivert indeks har
-- ingen allokerte sider, så komprimeringsinnstillingen er ikke til å stole på,
-- og uten dette går ObservationTaxonHierarchy fra ~10 GB til ~21 GB.
--
-- Én om gangen med MAXDOP = 4 for å holde loggpresset nede.
-- ---------------------------------------------------------------------------
RAISERROR('=== Gjenoppbygger indekser ===', 0, 1) WITH NOWAIT;

DECLARE @IdxName SYSNAME, @IdxTable NVARCHAR(300);

DECLARE idx_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT i.name,
           QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id))
    FROM sys.indexes i
    WHERE i.object_id IN (OBJECT_ID('dbo.ObservationEntityIndex'),
                          OBJECT_ID('dbo.ObservationTaxonHierarchy'),
                          OBJECT_ID('dbo.ObservationDataset'))
      AND i.type = 2
      AND i.is_disabled = 1
    ORDER BY i.name;

OPEN idx_cursor;
FETCH NEXT FROM idx_cursor INTO @IdxName, @IdxTable;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | Gjenoppbygger ', @IdxName, '...');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @Sql = N'ALTER INDEX ' + QUOTENAME(@IdxName) + N' ON ' + @IdxTable
             + N' REBUILD WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);';
    EXEC sp_executesql @Sql;

    FETCH NEXT FROM idx_cursor INTO @IdxName, @IdxTable;
END

CLOSE idx_cursor;
DEALLOCATE idx_cursor;

-- ---------------------------------------------------------------------------
-- Opprett columnstore-indeksen med full kolonneliste.
--
-- Dette er det ENESTE stedet IX_OEI_Columnstore bygges etter CompleteFilter.
-- Den ble sluppet øverst i skriptet, og bygges her én gang — med kolonnene
-- ferdig fylt av seksjonene over. 10-30 minutter på 192M rader.
--
-- ObservationId er med i kolonnelisten med vilje. Kolonnen har høy kardinalitet,
-- som normalt komprimerer dårlig, men tabellen er clustered på ObservationId, så
-- radene ligger fysisk sortert og hver rowgroup dekker et sammenhengende
-- ID-intervall som delta-encoding håndterer godt. Uten kolonnen kunne ikke
-- spørringer som joiner mot Observation betjenes herfra.
--
-- Kommer det flere filterkolonner senere, må de inn i denne listen — ellers
-- filtreres de fra rowstore og er trege.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore (10-30 min)...', 0, 1) WITH NOWAIT;

    CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
    ON dbo.ObservationEntityIndex
        (ObservationId, EntityTypeId, EntityId,
         TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
         HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
         SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
         InstitutionOrgId, CollectionOrgId, BehaviorId)
    WITH (MAXDOP = 4);

    RAISERROR('IX_OEI_Columnstore opprettet.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('IX_OEI_Columnstore finnes allerede - hopper over.', 0, 1) WITH NOWAIT;


-- ---------------------------------------------------------------------------
-- Oppdater statistikk.
--
-- DETTE ER IKKE VALGFRITT. Seksjon C, D og E skriver om flere hundre millioner
-- kolonneverdier. Indeksgjenoppbyggingen over oppdaterer statistikken for
-- indeksnøklene, men de auto-opprettede kolonnestatistikkene (_WA_Sys_*) på
-- CategoryId, BasisOfRecordId, InstitutionOrgId osv. henger ikke på noen av de
-- indeksene, og blir dermed stående urørt.
--
-- Målt konsekvens av å hoppe over dette: optimalisereren planla mot statistikk
-- som var en uke gammel og 68 millioner endringer på etterskudd, og trodde
-- fortsatt tabellen hadde 192 112 940 rader. Kategori-filteret gikk fra 473 ms
-- til 11 982 ms — ikke fordi designet var feil, men fordi planen var det.
--
-- FULLSCAN med vilje: tabellen er nettopp omskrevet, og et sample ville gitt
-- histogrammer som ikke reflekterer den nye fordelingen i kolonnene. Regn med
-- 10-20 minutter på 195M rader.
-- ---------------------------------------------------------------------------
-- Statistikken har SITT EGET vannmerke, ikke @HarArbeid.
--
-- @HarArbeid svarer bare paa "er alle rader fylt?". Feiler skriptet ETTER
-- seksjonene - tidsavbrudd, drept agent, mistet forbindelse - staar alle
-- vannmerkene paa @MaxId, og neste forsoek konkluderte med "ingenting aa gjoere"
-- og hoppet over statistikken. Migrasjonen ble stemplet som anvendt paa under et
-- minutt, med statistikk som fortsatt beskrev tabellen slik den saa ut FOER
-- backfillen. Maalt konsekvens staar i kommentaren over: 473 ms -> 11 982 ms.
--
-- Det skjedde i praksis 31. august 2026: kjoringen doede i UPDATE STATISTICS paa
-- Observation, og gjenkjoeringen rapporterte OK uten aa roere statistikken.
--
-- F_Statistics skrives foerst NAAR alle fire er ferdige, saa et avbrudd midt i
-- lar neste forsoek gjoere dem om igjen.
DECLARE @TrengerStatistikk BIT = 0;

IF @MaxId >= @MinId AND (
       @HarArbeid = 1
    OR ISNULL((SELECT p.LastCompletedId FROM dbo.BackfillProgress p
               WHERE p.Section = 'F_Statistics'), @MinId - 1) < @MaxId)
    SET @TrengerStatistikk = 1;

IF @TrengerStatistikk = 1
BEGIN
    RAISERROR('Oppdaterer statistikk med FULLSCAN (10-20 min)...', 0, 1) WITH NOWAIT;

    UPDATE STATISTICS dbo.ObservationEntityIndex WITH FULLSCAN;
    RAISERROR('  ObservationEntityIndex ferdig.', 0, 1) WITH NOWAIT;

    UPDATE STATISTICS dbo.Observation WITH FULLSCAN;
    RAISERROR('  Observation ferdig.', 0, 1) WITH NOWAIT;

    UPDATE STATISTICS dbo.ObservationDataset WITH FULLSCAN;
    RAISERROR('  ObservationDataset ferdig.', 0, 1) WITH NOWAIT;

    UPDATE STATISTICS dbo.ObservationTaxonHierarchy WITH FULLSCAN;
    RAISERROR('  ObservationTaxonHierarchy ferdig.', 0, 1) WITH NOWAIT;

    -- Foerst her regnes etterarbeidet som fullfoert.
    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'F_Statistics' AS Section, @MaxId AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    RAISERROR('Statistikk oppdatert.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Statistikken er allerede oppdatert for denne datamengden - hopper over.', 0, 1) WITH NOWAIT;

SET @Msg = CONCAT('BackfillAll ferdig. Total tid: ',
                  DATEDIFF(MINUTE, @RunStart, SYSUTCDATETIME()), ' minutter.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ---------------------------------------------------------------------------
-- Verifisering — «det kjørte» er ikke det samme som «det er riktig».
-- Feiler denne, er dataene ufullstendige og filtrene svarer feil.
-- ---------------------------------------------------------------------------
-- HVER KONTROLL MÅ VÆRE OPPFYLLBAR. Speil seksjonens oppdateringspredikat, ikke
-- «kolonnen er NULL» — en kolonne kan være NULL fordi kilden ikke har noen verdi,
-- og da er raden ferdig behandlet. En kontroll som ikke kan nå null gjør at
-- migrasjonen aldri registreres som anvendt og kjøres om igjen ved hver deploy.
--
-- Alle kontroller joiner mot kilden, slik at foreldreløse indeksrader (rader
-- hvis ObservationId ikke lenger finnes i Observation) ikke telles. Ingen seksjon
-- kan nå dem, så de ville ellers blokkert for alltid.
DECLARE @Kontroller TABLE (Nr INT, Navn NVARCHAR(80), Gjenstaar BIGINT);

INSERT INTO @Kontroller (Nr, Navn, Gjenstaar)
SELECT 1, 'C: OEI denormaliserte filterkolonner',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        WHERE idx.TaxonGroupId = 0)
UNION ALL
SELECT 2, 'D: OEI taksonrangkolonner',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
        WHERE (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
           OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
           OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
           OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL))
UNION ALL
SELECT 3, 'E1: Observation.InstitutionOrgId',
       (SELECT COUNT_BIG(*) FROM dbo.Observation o
        WHERE o.InstitutionOrgId IS NULL
          AND EXISTS (SELECT 1 FROM dbo.OrganizationRelation r
                      JOIN dbo.Organization g ON g.Id = r.OrganizationId
                      WHERE r.ObservationId = o.Id AND g.OrganizationTypeId = 1))
UNION ALL
SELECT 4, 'E1: Observation.CollectionOrgId',
       (SELECT COUNT_BIG(*) FROM dbo.Observation o
        WHERE o.CollectionOrgId IS NULL
          AND EXISTS (SELECT 1 FROM dbo.OrganizationRelation r
                      JOIN dbo.Organization g ON g.Id = r.OrganizationId
                      WHERE r.ObservationId = o.Id AND g.OrganizationTypeId = 2))
UNION ALL
-- E2 ble ikke kontrollert i det hele tatt tidligere. ObservationDataset er den
-- eneste kilden til datasett-tilknytning etter at OrganizationRelation slippes,
-- så en tom eller delvis fylt tabell her er permanent datatap.
SELECT 5, 'E2: ObservationDataset',
       (SELECT COUNT_BIG(*) FROM dbo.OrganizationRelation r
        JOIN dbo.Organization g ON g.Id = r.OrganizationId AND g.OrganizationTypeId = 3
        WHERE r.ObservationId BETWEEN @MinId AND @MaxId
          AND NOT EXISTS (SELECT 1 FROM dbo.ObservationDataset d
                          WHERE d.ObservationId = r.ObservationId
                            AND d.DatasetOrgId  = r.OrganizationId))
UNION ALL
SELECT 6, 'E3: OEI InstitutionOrgId/CollectionOrgId',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        WHERE (idx.InstitutionOrgId IS NULL AND o.InstitutionOrgId IS NOT NULL)
           OR (idx.CollectionOrgId  IS NULL AND o.CollectionOrgId  IS NOT NULL))
UNION ALL
-- BehaviorId kan ikke kontrolleres med «IS NULL»: ~73 % av observasjonene har
-- ingen atferd, og NULL er da riktig sluttilstand. Kilden avgjør.
SELECT 7, 'E4: OEI BehaviorId',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationBehaviors b ON b.ObservationId = idx.ObservationId
        WHERE idx.BehaviorId IS NULL);

DECLARE @Mangler BIGINT = (SELECT SUM(Gjenstaar) FROM @Kontroller);

SELECT Nr, Navn, Gjenstaar,
       CASE WHEN Gjenstaar = 0 THEN 'OK' ELSE 'MANGLER' END AS Status
FROM @Kontroller ORDER BY Nr;

IF @Mangler > 0
BEGIN
    -- Skriv ut hver enkelt kontroll som feilet. Summen alene sier ingenting om
    -- hvor problemet ligger, og dette er meldingen som faktisk blir lest.
    DECLARE @Nr INT, @Navn NVARCHAR(80), @Ant BIGINT;
    DECLARE feil_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Nr, Navn, Gjenstaar FROM @Kontroller WHERE Gjenstaar > 0 ORDER BY Nr;
    OPEN feil_cursor;
    FETCH NEXT FROM feil_cursor INTO @Nr, @Navn, @Ant;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Msg = CONCAT('  MANGLER - kontroll ', @Nr, ' (', @Navn, '): ',
                          FORMAT(@Ant, 'N0'), ' rader');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        FETCH NEXT FROM feil_cursor INTO @Nr, @Navn, @Ant;
    END
    CLOSE feil_cursor;
    DEALLOCATE feil_cursor;

    SET @Msg = CONCAT('UFULLSTENDIG: ', FORMAT(@Mangler, 'N0'),
                      ' rader gjenstaar totalt. Kjoer skriptet paa nytt - det fortsetter der det slapp.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - alle backfills er komplette.', 0, 1) WITH NOWAIT;

SELECT Section, LastCompletedId, UpdatedAt FROM dbo.BackfillProgress ORDER BY Section;

/*
    Fyller de 19 nye taksonrangkolonnene på ObservationEntityIndex fra
    ObservationTaxonHierarchy, og bygger IX_OEI_Columnstore på nytt med full
    kolonneliste.

    HVORFOR DETTE ER ET EGET SKRIPT OG IKKE BackfillAll.sql
    BackfillAll har en preflight som stopper når dbo.OrganizationRelation eller
    dbo.ObservationDataset mangler, med meldingen «Oppryddingsmigrasjonen har alt
    kjoert, og dette skriptet kan ikke lenger brukes til reparasjon». Det er riktig:
    seksjonene E1, E2 og E3 leser nettopp de tabellene, og i miljøer som har passert
    opprydningen finnes de ikke. BackfillAll er dermed ikke kjørbart der —
    verifisert mot Artskart3IndexProdLikeTestMigrations 17.11.2026.

    Seksjon D trenger ingen av dem. Den leser ObservationTaxonHierarchy og skriver
    til ObservationEntityIndex, og begge finnes. Dette skriptet er derfor seksjon D
    og indekshåndteringen hennes, løftet ut.

    BackfillAll seksjon D er oppdatert til å fylle alle 23 rangene, så et FRISKT
    miljø som kjører BackfillAll trenger ikke dette skriptet. Det er for miljøer der
    kolonnene kom etter opprydningen.

    FYLLER BARE DE 19 NYE, IKKE ALLE 23
    De fire gamle — art, slekt, familie, orden — er alt fylt der dette skriptet er
    relevant, og de ligger i IX_OEI_AreaListView. Å skrive dem om ville tvunget
    vedlikehold av en bred nonclustered indeks over 134,7 millioner rader, for
    verdier som ikke endrer seg. Ingen av de 19 nye er i noen rowstore-indeks, så
    oppdateringen rører bare klyngeindeksen.

    Preflighten sjekker at de fire faktisk ER fylt, slik at antakelsen ikke blir
    stående taus.

    EGET VANNMERKE
    'D19_EntityIndexNewTaxonRanks', ikke BackfillAll sitt
    'D_EntityIndexAllTaxonRanks'. Delte de nøkkel, ville en senere BackfillAll-kjøring
    sett arbeidet som gjort og hoppet over de fire gamle kolonnene i et miljø der de
    manglet.

    IDEMPOTENT. Kan avbrytes og kjøres om igjen; vannmerket skrives per batch, og
    predikatet treffer bare rader som mangler en verdi kilden har.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @BatchSize INT = 500000;
DECLARE @Start DATETIME2 = SYSUTCDATETIME();
DECLARE @SectionStart DATETIME2;
DECLARE @T DATETIME2, @Msg NVARCHAR(500), @Rows BIGINT, @Total BIGINT;
DECLARE @CurrentId INT, @BatchEnd INT, @MinId INT, @MaxId INT;

-- ---------------------------------------------------------------------------
-- Preflight
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.ObservationEntityIndex') IS NULL
    OR OBJECT_ID('dbo.ObservationTaxonHierarchy') IS NULL
BEGIN
    RAISERROR('AVBRUTT: ObservationEntityIndex eller ObservationTaxonHierarchy mangler.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- Alle 23 rangkolonnene må finnes. Mangler én, er migrasjonen
-- AddAllTaxonRankColumnsToObservationEntityIndex ikke kjørt, og skriptet ville
-- feilet midt i en flere timer lang kjøring i stedet for her.
IF COL_LENGTH('dbo.ObservationEntityIndex', 'KingdomTaxonId')    IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'ClassTaxonId')      IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'SubspeciesTaxonId') IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'VarietyTaxonId')    IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'FormTaxonId')       IS NULL
    OR COL_LENGTH('dbo.ObservationEntityIndex', 'SectionTaxonId')    IS NULL
BEGIN
    RAISERROR('AVBRUTT: taksonrangkolonnene mangler. Kjoer migrasjonen AddAllTaxonRankColumnsToObservationEntityIndex foerst.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- ANTAKELSEN OM DE FIRE GAMLE, SJEKKET.
--
-- Skriptet rører dem ikke, fordi de skal være fylt alt. Er de ikke det, fyller
-- ingenting dem her, og indeksen blir stående ufullstendig uten at noe sier fra.
-- Da må BackfillAll seksjon D kjøres i stedet — eller dette skriptet utvides.
DECLARE @GamleMangler BIGINT = (
    SELECT COUNT_BIG(*)
    FROM dbo.ObservationEntityIndex idx
    JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
    WHERE (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
       OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
       OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
       OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL));

IF @GamleMangler > 0
BEGIN
    SET @Msg = CONCAT('AVBRUTT: ', FORMAT(@GamleMangler, 'N0'), ' rader mangler art/slekt/',
                      'familie/orden. Dette skriptet fyller bare de 19 nye rangene og ',
                      'forutsetter at de fire gamle er fylt. Kjoer BackfillAll seksjon D.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
    RETURN;
END

SELECT @MinId = MIN(ObservationId), @MaxId = MAX(ObservationId) FROM dbo.ObservationEntityIndex;

IF @MinId IS NULL
BEGIN
    RAISERROR('ObservationEntityIndex er tom - ingenting aa fylle.', 0, 1) WITH NOWAIT;
    RETURN;
END

IF OBJECT_ID('dbo.BackfillProgress') IS NULL
BEGIN
    RAISERROR('AVBRUTT: dbo.BackfillProgress mangler.', 16, 1) WITH NOWAIT;
    RETURN;
END

SET @Msg = CONCAT('ObservationId-intervall: ', FORMAT(@MinId, 'N0'), ' - ', FORMAT(@MaxId, 'N0'),
                  ' (', FORMAT((@MaxId - @MinId) / @BatchSize + 1, 'N0'), ' batcher)');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Columnstore slippes, den deaktiveres ikke.
--
-- Indeksen må uansett opprettes på nytt: en columnstore-indeks kan ikke utvides
-- med ALTER, og de 19 nye kolonnene skal inn i kolonnelisten. Ved å slippe den nå
-- og opprette den til slutt, bygges den nøyaktig én gang — med data på plass.
--
-- Oppdateringen går dessuten langt raskere uten den: en nonclustered columnstore
-- over rader som endres gir sletteflagg og delta-lagring per rad.
--
-- Konsekvens: indeksen finnes ikke mens skriptet går, og områdetellinger,
-- listevisning og fasett-tall er merkbart tregere i det vinduet. Samme forbehold
-- som BackfillAll har.
--
-- Rowstore-indeksene røres IKKE. Ingen av de 19 kolonnene er i dem, så de trenger
-- ikke vedlikehold — og IX_OEI_AreaListView er bred nok at en ombygging ville
-- kostet mer enn hele fyllingen.
-- ---------------------------------------------------------------------------
DECLARE @Gjenstaar BIGINT = (
    SELECT ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                   WHERE Section = 'D19_EntityIndexNewTaxonRanks'), @MinId - 1));

IF @Gjenstaar < @MaxId AND EXISTS (SELECT 1 FROM sys.indexes
        WHERE name = 'IX_OEI_Columnstore'
          AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Slipper IX_OEI_Columnstore (opprettes paa nytt til slutt)...', 0, 1) WITH NOWAIT;
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;
END

-- ---------------------------------------------------------------------------
-- Fyllingen
-- ---------------------------------------------------------------------------
RAISERROR('=== Fyller de 19 nye taksonrangkolonnene ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'D19_EntityIndexNewTaxonRanks'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over fyllingen.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;
    SET @T = SYSUTCDATETIME();

    UPDATE idx
    SET idx.KingdomTaxonId     = h.KingdomTaxonId,
        idx.SubkingdomTaxonId  = h.SubkingdomTaxonId,
        idx.PhylumTaxonId      = h.PhylumTaxonId,
        idx.SubphylumTaxonId   = h.SubphylumTaxonId,
        idx.SuperclassTaxonId  = h.SuperclassTaxonId,
        idx.ClassTaxonId       = h.ClassTaxonId,
        idx.SubclassTaxonId    = h.SubclassTaxonId,
        idx.InfraclassTaxonId  = h.InfraclassTaxonId,
        idx.SuperorderTaxonId  = h.SuperorderTaxonId,
        idx.SuborderTaxonId    = h.SuborderTaxonId,
        idx.InfraorderTaxonId  = h.InfraorderTaxonId,
        idx.SuperfamilyTaxonId = h.SuperfamilyTaxonId,
        idx.SubfamilyTaxonId   = h.SubfamilyTaxonId,
        idx.TribeTaxonId       = h.TribeTaxonId,
        idx.SubgenusTaxonId    = h.SubgenusTaxonId,
        idx.SectionTaxonId     = h.SectionTaxonId,
        idx.SubspeciesTaxonId  = h.SubspeciesTaxonId,
        idx.VarietyTaxonId     = h.VarietyTaxonId,
        idx.FormTaxonId        = h.FormTaxonId
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
    WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
      -- OPPFYLLBART PREDIKAT: treffer bare rader der kilden har en verdi indeksen
      -- mangler, så raden ikke matcher etter oppdateringen. Samme regel som
      -- BackfillAll, og av samme grunn — verifiseringen nederst bruker predikatet,
      -- og en kontroll som ikke kan nå null ville aldri sagt «ferdig».
      --
      -- Listen må speile SET-listen over, og den samme listen står i
      -- verifiseringen. Tre steder; går de fra hverandre, stopper fyllingen for
      -- tidlig eller kan ikke bli ferdig.
      AND (   (idx.KingdomTaxonId     IS NULL AND h.KingdomTaxonId     IS NOT NULL)
           OR (idx.SubkingdomTaxonId  IS NULL AND h.SubkingdomTaxonId  IS NOT NULL)
           OR (idx.PhylumTaxonId      IS NULL AND h.PhylumTaxonId      IS NOT NULL)
           OR (idx.SubphylumTaxonId   IS NULL AND h.SubphylumTaxonId   IS NOT NULL)
           OR (idx.SuperclassTaxonId  IS NULL AND h.SuperclassTaxonId  IS NOT NULL)
           OR (idx.ClassTaxonId       IS NULL AND h.ClassTaxonId       IS NOT NULL)
           OR (idx.SubclassTaxonId    IS NULL AND h.SubclassTaxonId    IS NOT NULL)
           OR (idx.InfraclassTaxonId  IS NULL AND h.InfraclassTaxonId  IS NOT NULL)
           OR (idx.SuperorderTaxonId  IS NULL AND h.SuperorderTaxonId  IS NOT NULL)
           OR (idx.SuborderTaxonId    IS NULL AND h.SuborderTaxonId    IS NOT NULL)
           OR (idx.InfraorderTaxonId  IS NULL AND h.InfraorderTaxonId  IS NOT NULL)
           OR (idx.SuperfamilyTaxonId IS NULL AND h.SuperfamilyTaxonId IS NOT NULL)
           OR (idx.SubfamilyTaxonId   IS NULL AND h.SubfamilyTaxonId   IS NOT NULL)
           OR (idx.TribeTaxonId       IS NULL AND h.TribeTaxonId       IS NOT NULL)
           OR (idx.SubgenusTaxonId    IS NULL AND h.SubgenusTaxonId    IS NOT NULL)
           OR (idx.SectionTaxonId     IS NULL AND h.SectionTaxonId     IS NOT NULL)
           OR (idx.SubspeciesTaxonId  IS NULL AND h.SubspeciesTaxonId  IS NOT NULL)
           OR (idx.VarietyTaxonId     IS NULL AND h.VarietyTaxonId     IS NOT NULL)
           OR (idx.FormTaxonId        IS NULL AND h.FormTaxonId        IS NOT NULL))
    OPTION (RECOMPILE);

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    -- Vannmerket settes ETTER batchen. Uten omsluttende transaksjon er batchen alt
    -- committet, så vannmerket er sant i det øyeblikket det skrives.
    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'D19_EntityIndexNewTaxonRanks' AS Section, @BatchEnd AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

    SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | ',
                      FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                      ' | Oppdatert: ', FORMAT(@Rows, 'N0'),
                      ' | Totalt: ', FORMAT(@Total, 'N0'),
                      ' | batch ', DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's',
                      ' | samlet ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

-- ---------------------------------------------------------------------------
-- Verifisering — «det kjørte» er ikke det samme som «det er riktig».
--
-- Speiler oppdateringspredikatet, ikke «kolonnen er NULL»: en kolonne kan være
-- NULL fordi kilden ikke har noen verdi, og da er raden ferdig behandlet.
--
-- Joiner mot kilden, slik at foreldreløse indeksrader ikke telles — ingen fylling
-- kan nå dem, og de ville ellers blokkert for alltid.
-- ---------------------------------------------------------------------------
RAISERROR('=== Verifiserer ===', 0, 1) WITH NOWAIT;

DECLARE @Ufylt BIGINT = (
    SELECT COUNT_BIG(*)
    FROM dbo.ObservationEntityIndex idx
    JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
    WHERE (idx.KingdomTaxonId     IS NULL AND h.KingdomTaxonId     IS NOT NULL)
       OR (idx.SubkingdomTaxonId  IS NULL AND h.SubkingdomTaxonId  IS NOT NULL)
       OR (idx.PhylumTaxonId      IS NULL AND h.PhylumTaxonId      IS NOT NULL)
       OR (idx.SubphylumTaxonId   IS NULL AND h.SubphylumTaxonId   IS NOT NULL)
       OR (idx.SuperclassTaxonId  IS NULL AND h.SuperclassTaxonId  IS NOT NULL)
       OR (idx.ClassTaxonId       IS NULL AND h.ClassTaxonId       IS NOT NULL)
       OR (idx.SubclassTaxonId    IS NULL AND h.SubclassTaxonId    IS NOT NULL)
       OR (idx.InfraclassTaxonId  IS NULL AND h.InfraclassTaxonId  IS NOT NULL)
       OR (idx.SuperorderTaxonId  IS NULL AND h.SuperorderTaxonId  IS NOT NULL)
       OR (idx.SuborderTaxonId    IS NULL AND h.SuborderTaxonId    IS NOT NULL)
       OR (idx.InfraorderTaxonId  IS NULL AND h.InfraorderTaxonId  IS NOT NULL)
       OR (idx.SuperfamilyTaxonId IS NULL AND h.SuperfamilyTaxonId IS NOT NULL)
       OR (idx.SubfamilyTaxonId   IS NULL AND h.SubfamilyTaxonId   IS NOT NULL)
       OR (idx.TribeTaxonId       IS NULL AND h.TribeTaxonId       IS NOT NULL)
       OR (idx.SubgenusTaxonId    IS NULL AND h.SubgenusTaxonId    IS NOT NULL)
       OR (idx.SectionTaxonId     IS NULL AND h.SectionTaxonId     IS NOT NULL)
       OR (idx.SubspeciesTaxonId  IS NULL AND h.SubspeciesTaxonId  IS NOT NULL)
       OR (idx.VarietyTaxonId     IS NULL AND h.VarietyTaxonId     IS NOT NULL)
       OR (idx.FormTaxonId        IS NULL AND h.FormTaxonId        IS NOT NULL));

IF @Ufylt > 0
BEGIN
    SET @Msg = CONCAT('VERIFISERING FEILET: ', FORMAT(@Ufylt, 'N0'),
                      ' rader mangler fortsatt en rangverdi kilden har. ',
                      'Columnstore bygges IKKE - kjoer skriptet om igjen.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
    RETURN;
END

RAISERROR('Verifisering OK - alle 19 rangkolonner er fylt.', 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Klyngeindeksen bygges om — IKKE valgfritt vedlikehold.
--
-- Fyllingen la 19 int-kolonner, opptil 76 byte, paa hver av 134,7 millioner
-- rader. Rader som alt laa paa fulle sider maa splittes, og det skjer over hele
-- indeksen. Maalt 18.11.2026 rett etter fyllingen:
--
--   PK_ObservationEntityIndex                98,3 % fragmentert
--   IX_ObservationEntityIndex_EntityLookup    0,0 %
--   IX_OEI_AreaListView                       0,0 %
--
-- De to nonclustered er urorte fordi ingen av de 19 kolonnene ligger i dem —
-- det var hele poenget med aa la dem staa. Men klyngeindeksen er selve
-- tabellen, og den kan ikke skaanes paa samme vis.
--
-- HVA DET KOSTET AA HOPPE OVER DETTE
-- Ytelsessuiten ble startet rett etter fyllingen og maalte Locations/oslo til
-- 14 508 ms mot 1 369 ms i baselinen fra samme commit — 10,6 ganger tregere.
-- Hele kjoeringen pacet mot ~30 timer i stedet for 43 minutter. Tallene var
-- ubrukelige, ikke bare daarlige.
--
-- MERK at BackfillAll.sql seksjon D har samme problem: den bygger bare om
-- DEAKTIVERTE NONCLUSTERED indekser (DATA_COMPRESSION = PAGE), aldri klyngen.
-- Et friskt miljoe som kjoerer BackfillAll ender derfor like fragmentert.
--
-- PAGE beholder komprimeringen indeksen alt har; uten den ville REBUILD
-- stilltiende dekomprimert tabellen.
--
-- Ligger FOER columnstore-byggingen, slik at den leser en ryddig klyngeindeks.
-- ---------------------------------------------------------------------------
RAISERROR('Bygger om PK_ObservationEntityIndex (~8,5 GB)...', 0, 1) WITH NOWAIT;
SET @T = SYSUTCDATETIME();

ALTER INDEX PK_ObservationEntityIndex ON dbo.ObservationEntityIndex
    REBUILD WITH (DATA_COMPRESSION = PAGE, MAXDOP = 4);

SET @Msg = CONCAT('PK_ObservationEntityIndex bygget om paa ',
                  DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- ---------------------------------------------------------------------------
-- Columnstore med full kolonneliste — 42 kolonner.
--
-- MÅ STEMME med listen i BackfillAll.sql. Går de fra hverandre, får et miljø som
-- kjørte det ene skriptet en annen indeks enn et som kjørte det andre, og en
-- kolonne utenfor listen filtreres fra rowstore og er treg.
--
-- Målt 17.11.2026 over 134 705 341 rader: 645 s aa bygge, 1 083 MB ferdig.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore med 42 kolonner (~11 min)...', 0, 1) WITH NOWAIT;
    SET @T = SYSUTCDATETIME();

    CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
    ON dbo.ObservationEntityIndex
        -- LocationId MAA vaere med. Locations-endepunktene grupperer paa den, og
        -- den finnes ellers bare i klyngeindeksen - ikke som include i
        -- IX_OEI_AreaListView. Uten den her maa planen skanne klyngeindeksen, og
        -- da blir spoerringen tregere for hver kolonne som legges paa tabellen.
        -- Maalt 18.11.2026: Locations/oslo 7 889 ms uten, se nedenfor.
        (ObservationId, EntityTypeId, EntityId,
         LocationId,
         TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
         HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
         InstitutionOrgId, DatasetOrgId, BehaviorId,
         -- FilterMask-bitene og maaneden. Uten dem kan ikke columnstore dekke
         -- aggregeringen, og planen faller til en skanning av klyngeindeksen -
         -- maalt 18.11.2026 til 55,4 millioner rader og 19-21 s per kall for
         -- AreaCounts med fire tunge filtre, mot 901 ms i baselinen. Hvert case
         -- som regredierte inneholdt minst ett av disse filtrene.
         ChangedByAnnotation, Introduced, Sensitive, UncertainIdentification,
         MonthCollected,
         KingdomTaxonId, SubkingdomTaxonId, PhylumTaxonId, SubphylumTaxonId,
         SuperclassTaxonId, ClassTaxonId, SubclassTaxonId, InfraclassTaxonId,
         SuperorderTaxonId, OrderTaxonId, SuborderTaxonId, InfraorderTaxonId,
         SuperfamilyTaxonId, FamilyTaxonId, SubfamilyTaxonId, TribeTaxonId,
         SubgenusTaxonId, GenusTaxonId, SectionTaxonId, SpeciesTaxonId,
         SubspeciesTaxonId, VarietyTaxonId, FormTaxonId)
    WITH (MAXDOP = 4);

    SET @Msg = CONCAT('IX_OEI_Columnstore opprettet paa ',
                      DATEDIFF(SECOND, @T, SYSUTCDATETIME()), 's.');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('IX_OEI_Columnstore finnes allerede - hopper over.', 0, 1) WITH NOWAIT;

-- FULLSCAN, ikke standard utvalg: tabellen har nettopp fått 19 nye kolonner, og
-- utvalgsstatistikk på en tabell i denne størrelsesordenen har før gjort
-- LocationCount sju ganger tregere.
RAISERROR('Oppdaterer statistikk (FULLSCAN)...', 0, 1) WITH NOWAIT;
UPDATE STATISTICS dbo.ObservationEntityIndex WITH FULLSCAN;

SET @Msg = CONCAT('Ferdig paa ', DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

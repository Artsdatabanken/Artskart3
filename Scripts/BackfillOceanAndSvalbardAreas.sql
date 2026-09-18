-- ============================================================================
-- BackfillOceanAndSvalbardAreas — målrettet reparasjon av ObservationEntityIndex
-- for AreaTypeId 4 (havområde) og 6 (Svalbard/Bjørnøya/Jan Mayen).
--
-- ENGANGSVERKTØY FOR MANUELL KJØRING. Dette er ikke en del av den vanlige
-- flyten og skal ikke inn i release-pipelinen. BackfillAll er fortsatt den
-- autoritative backfillen; dette skriptet finnes fordi en full kjøring av den
-- ville tatt timer for å reparere to områdetyper.
--
-- BAKGRUNN
-- Area ble oppdatert i produksjon etter at backfillene hadde kjørt. Fylker,
-- kommuner og verneområder har uendret Id og geometri, så deres rader i
-- indeksen er fortsatt korrekte. Det som mangler er type 6, og type 4 har fått
-- endringer.
--
-- Grunnen til at ingen oppdaget det: verifiseringsblokken i BackfillAll har sju
-- kontroller, og ingen av dem gjelder seksjon A. Den måler om eksisterende rader
-- har fått fylt kolonnene sine, ikke om radene som burde finnes faktisk finnes.
-- Kjøringen var derfor grønn med en ufullstendig Area-tabell.
--
-- HVA SKRIPTET GJØR — kun for EntityTypeId 4 og 6:
--   Steg 1  sletter rader som ikke lenger kan utledes av kilden
--   Steg 2  setter inn rader som mangler
--   Steg 3  fyller de denormaliserte filterkolonnene   (tilsvarer seksjon C)
--   Steg 4  fyller taksonrangkolonnene                 (tilsvarer seksjon D)
--   Steg 5  fyller InstitutionOrgId/DatasetOrgId    (tilsvarer seksjon E3)
--   Steg 6  fyller BehaviorId                          (tilsvarer seksjon E4)
--   Steg 7  oppdaterer statistikk og verifiserer
--
-- RØRER IKKE: seksjon B (ObservationTaxonHierarchy), E1 (Observation-kolonner)
-- og E2 (ObservationDataset). De ligger i andre tabeller og er upåvirket av Area.
--
-- RØRER IKKE BackfillAll SINE VANNMERKER. Skriptet fører sine egne, med prefiks
-- «X46_», slik at en senere kjøring av BackfillAll ikke tror den har mer arbeid
-- enn den har. @HarArbeid i BackfillAll leser en fast liste av seksjonsnavn og
-- ignorerer disse.
--
-- KAN AVBRYTES OG STARTES PÅ NYTT. Hvert steg har eget vannmerke i
-- dbo.BackfillProgress og hopper til der forrige kjøring stoppet.
--
-- MÅ KJØRE UTEN OMSLUTTENDE TRANSAKSJON. Med alt i én transaksjon ruller en
-- timeout tilbake hele kjøringen.
--
-- INDEKSER OG COLUMNSTORE HÅNDTERES IKKE. BackfillAll deaktiverer 28 rowstore-
-- indekser og slipper columnstore fordi den skriver over 131M rader. Her er
-- volumet en brøkdel av det, og et gjenoppbygg à 10-30 minutter ville kostet mer
-- enn indeksvedlikeholdet under innsettingen. Nye rader havner i columnstorens
-- deltastore; indeksen forblir korrekt, bare litt fragmentert. Se @Reorganiser.
--
-- ============================================================================
-- KJØR FØRST MED @DryRun = 1. Da skriver skriptet ingenting, men rapporterer
-- hvor mange rader hvert steg ville berørt.
--
-- MERK at steg 3-6 UNDERRAPPORTERER i tørrkjøring. De teller rader som mangler
-- kolonneverdier i dag, men radene steg 2 ville satt inn finnes jo ikke ennå.
-- Er steg 2 stort, blir steg 3-6 tilsvarende større enn tørrkjøringen viser.
-- Steg 1 og 2 er derimot nøyaktige, og det er de som avgjør om tallene ser
-- riktige ut.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ---------------------------------------------------------------------------
-- Innstillinger
-- ---------------------------------------------------------------------------
DECLARE @DryRun      BIT = 0;     -- 1 = rapporter bare, ikke skriv
DECLARE @UtfoerSletting BIT = 1;  -- 0 = hopp over steg 1 (kun innsetting)
DECLARE @Statistikk  BIT = 1;     -- oppdater statistikk til slutt
DECLARE @Reorganiser BIT = 0;     -- komprimer columnstore-deltastore til slutt
-- BEVISST MYE STOERRE ENN BackfillAll SINE 500 000.
--
-- BackfillAll batcher smaatt fordi den skriver 131M rader og maa la loggen
-- avkortes underveis. Her er skrivevolumet en broekdel, og da snur regnestykket:
-- hver batch koster en OPTION (RECOMPILE)-kompilering av en firetabells join med
-- NOT EXISTS, og den kostnaden dominerer.
--
-- Observation.Id er dessuten glissen - MAX(Id) er 113,9M mens tabellen har 61M
-- rader - saa 500 000 gir 228 batcher per steg, 1368 totalt over seks steg.
--
-- Maalt paa Artskart3IndexProdLikeTestMigrations (61M observasjoner):
--   steg 2 ubatchet, hele tabellen        6 797 ms
--   steg 1 ubatchet, hele tabellen        4 304 ms
--   steg 2, én batch paa 10M              2 694 ms
--   steg 2, én batch paa 500k (tett)         43 ms
-- En kjoering med 500k brukte over 10 minutter uten aa komme gjennom steg 2.
-- Med 10M blir det 12 batcher per steg og noen faa minutter totalt.
--
-- Resumerbarheten beholdes - vannmerket skrives fortsatt per batch.
DECLARE @BatchSize   INT = 10000000;

-- Områdetypene som repareres. Endres denne, endres alt skriptet gjør.
--
-- Temp-tabell, ikke tabellvariabel. Tabellvariabler har ingen statistikk, og
-- optimalisereren estimerer da én rad. Joinet mot en tabell på 131M rader er
-- det nok til å velge nested loops der den skulle valgt en scan, og en
-- målrettet reparasjon blir til en times kjøring.
DROP TABLE IF EXISTS #Typer;
CREATE TABLE #Typer (AreaTypeId INT PRIMARY KEY);
INSERT INTO #Typer (AreaTypeId) VALUES (4), (6);

DECLARE @RunStart DATETIME2 = SYSUTCDATETIME();
DECLARE @Msg      NVARCHAR(400);
DECLARE @MinId    INT, @MaxId INT, @CurrentId INT, @BatchEnd INT;
DECLARE @Rows     INT, @Total BIGINT;
DECLARE @SectionStart DATETIME2;

SELECT @MinId = MIN(Id), @MaxId = MAX(Id) FROM dbo.Observation;

IF @MinId IS NULL
BEGIN
    RAISERROR('Observation er tom - ingenting aa gjoere.', 0, 1) WITH NOWAIT;
    RETURN;
END

RAISERROR('=== BackfillOceanAndSvalbardAreas ===', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('ObservationId ', FORMAT(@MinId, 'N0'), ' - ', FORMAT(@MaxId, 'N0'),
                  ' | batch ', FORMAT(@BatchSize, 'N0'),
                  ' | DryRun=', @DryRun, ' Sletting=', @UtfoerSletting);
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

IF OBJECT_ID('dbo.BackfillProgress') IS NULL
BEGIN
    CREATE TABLE dbo.BackfillProgress (
        Section         VARCHAR(64)   NOT NULL PRIMARY KEY,
        LastCompletedId INT           NOT NULL,
        UpdatedAt       DATETIME2     NOT NULL CONSTRAINT DF_BackfillProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    RAISERROR('Opprettet dbo.BackfillProgress.', 0, 1) WITH NOWAIT;
END

-- ---------------------------------------------------------------------------
-- Områdeoppslaget.
--
-- Fid konverteres til EntityId nøyaktig som i seksjon A i BackfillAll. Prefikset
-- 'Naturbase VV' gjelder bare AreaTypeId 3, som ikke er med her, så CASE-en
-- reduseres til understrek-fjerningen. Historiske Fid-er som '15_2017' finnes i
-- praksis bare på fylker, men konverteringen beholdes for å være identisk med A.
--
-- Tabellen brukes for å slippe CAST i korrelerte predikater lenger nede. Area er
-- liten; ObservationEntityIndex er det ikke.
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS #Omr;

SELECT a.Id                                          AS AreaId,
       a.AreaTypeId                                  AS EntityTypeId,
       TRY_CAST(REPLACE(a.Fid, '_', '') AS INT)      AS EntityId,
       a.Fid                                         AS Fid,
       a.Name                                        AS Navn
INTO #Omr
FROM dbo.Area a
JOIN #Typer t ON t.AreaTypeId = a.AreaTypeId
WHERE a.IsCurrent = 1;

CREATE UNIQUE CLUSTERED INDEX PK_Omr ON #Omr (AreaId);
CREATE INDEX IX_Omr_Entity ON #Omr (EntityTypeId, EntityId);

-- ---------------------------------------------------------------------------
-- Forkontroll 1: Fid-er som ikke lar seg konvertere.
--
-- Seksjon A bruker CAST, ikke TRY_CAST. Finnes det slike rader, ville en
-- ordinaer backfill AVBRUTT - ikke gitt feil tall. Vi stopper her i stedet, med
-- en melding som sier hvilke rader det gjelder.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM #Omr WHERE EntityId IS NULL)
BEGIN
    RAISERROR('AVBRUTT: Fid-er som ikke er numeriske:', 0, 1) WITH NOWAIT;
    SELECT AreaId, EntityTypeId, Fid, Navn FROM #Omr WHERE EntityId IS NULL;
    RAISERROR('Rett opp Fid-verdiene i dbo.Area foer skriptet kjoeres.', 16, 1) WITH NOWAIT;
    RETURN;
END

-- ---------------------------------------------------------------------------
-- Forkontroll 2: to Fid-er som havner paa samme EntityId innenfor samme type.
--
-- Ville slaatt to omraader sammen til ett i indeksen - en stille dobbelttelling
-- som ingen av verifiseringene fanger, fordi noekkelen finnes paa begge sider.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM #Omr GROUP BY EntityTypeId, EntityId HAVING COUNT(*) > 1)
BEGIN
    RAISERROR('AVBRUTT: flere Area-rader gir samme (EntityTypeId, EntityId):', 0, 1) WITH NOWAIT;
    SELECT EntityTypeId, EntityId, COUNT(*) AS AntallAreaRader,
           STRING_AGG(Fid, ', ') AS Fider
    FROM #Omr GROUP BY EntityTypeId, EntityId HAVING COUNT(*) > 1;
    RAISERROR('Fid-konverteringen er ikke entydig for disse. Maa avklares foerst.', 16, 1) WITH NOWAIT;
    RETURN;
END

SELECT @Rows = COUNT(*) FROM #Omr;
SET @Msg = CONCAT('Omraader som repareres: ', @Rows);
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

SELECT EntityTypeId,
       CASE EntityTypeId WHEN 4 THEN 'Havomraade'
                         WHEN 6 THEN 'Svalbard/Bjoernoeya/JanMayen'
                         ELSE CONCAT('Type ', EntityTypeId) END AS Type,
       COUNT(*) AS Antall
FROM #Omr GROUP BY EntityTypeId ORDER BY EntityTypeId;


-- ===========================================================================
-- STEG 1: slett rader som ikke lenger kan utledes av kilden
--
-- Type 4 har fått endringer. Endringen kan vaere at omraader er pensjonert
-- (IsCurrent = 0), eller at LocationAreas peker et annet sted enn foer. Begge
-- deler etterlater rader i indeksen som ikke lenger stemmer, og BackfillAll
-- sletter aldri - den har ingen DELETE i det hele tatt.
--
-- Predikatet er «kan ikke utledes av kilden i dag», ikke «Area finnes ikke».
-- Det foerste dekker ogsaa tilfellet der omraadet fortsatt finnes, men
-- observasjonen ikke lenger ligger i det.
-- ===========================================================================
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 1: slett rader som ikke kan utledes ===', 0, 1) WITH NOWAIT;

IF @UtfoerSletting = 0
    RAISERROR('Hoppet over (@UtfoerSletting = 0).', 0, 1) WITH NOWAIT;
ELSE
BEGIN
    SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                                WHERE Section = 'X46_Delete'), @MinId - 1) + 1;
    SET @Total = 0;
    SET @SectionStart = SYSUTCDATETIME();

    IF @CurrentId > @MaxId
        RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

    WHILE @CurrentId <= @MaxId
    BEGIN
        SET @BatchEnd = @CurrentId + @BatchSize - 1;

        IF @DryRun = 1
            SELECT @Rows = COUNT(*)
            FROM dbo.ObservationEntityIndex idx
            JOIN #Typer t ON t.AreaTypeId = idx.EntityTypeId
            WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.Observation o
                    JOIN dbo.Location l       ON l.Id = o.LocationId
                    JOIN dbo.LocationAreas la ON la.LocationId = l.Id
                    JOIN #Omr g               ON g.AreaId = la.AreaId
                    WHERE o.Id = idx.ObservationId
                      AND g.EntityTypeId = idx.EntityTypeId
                      AND g.EntityId     = idx.EntityId);
        ELSE
        BEGIN
            DELETE idx
            FROM dbo.ObservationEntityIndex idx
            JOIN #Typer t ON t.AreaTypeId = idx.EntityTypeId
            WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.Observation o
                    JOIN dbo.Location l       ON l.Id = o.LocationId
                    JOIN dbo.LocationAreas la ON la.LocationId = l.Id
                    JOIN #Omr g               ON g.AreaId = la.AreaId
                    WHERE o.Id = idx.ObservationId
                      AND g.EntityTypeId = idx.EntityTypeId
                      AND g.EntityId     = idx.EntityId)
            OPTION (RECOMPILE);

            SET @Rows = @@ROWCOUNT;
        END

        SET @Total = @Total + @Rows;

        IF @DryRun = 0
            MERGE dbo.BackfillProgress AS t
            USING (SELECT 'X46_Delete' AS Section, @BatchEnd AS LastCompletedId) AS s
                ON t.Section = s.Section
            WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 1 slett | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

        SET @CurrentId = @BatchEnd + 1;
    END

    SET @Msg = CONCAT('Steg 1 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
END


-- ===========================================================================
-- STEG 2: sett inn manglende rader
--
-- Identisk join som seksjon A i BackfillAll, avgrenset til typene i #Typer.
-- Radene faar TaxonGroupId = 0 fra defaultverdien, som er markoeren steg 3
-- leter etter.
-- ===========================================================================
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 2: sett inn manglende rader ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'X46_Insert'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    IF @DryRun = 1
        SELECT @Rows = COUNT(*)
        FROM (
            SELECT DISTINCT o.Id AS ObservationId, g.EntityTypeId, g.EntityId
            FROM dbo.Observation o
            JOIN dbo.Location l       ON l.Id = o.LocationId
            JOIN dbo.LocationAreas la ON la.LocationId = l.Id
            JOIN #Omr g               ON g.AreaId = la.AreaId
            WHERE o.LocationId IS NOT NULL
              AND o.Id >= @CurrentId AND o.Id <= @BatchEnd
              AND NOT EXISTS (
                    SELECT 1 FROM dbo.ObservationEntityIndex x
                    WHERE x.ObservationId = o.Id
                      AND x.EntityTypeId  = g.EntityTypeId
                      AND x.EntityId      = g.EntityId)
        ) AS q;
    ELSE
    BEGIN
        INSERT INTO dbo.ObservationEntityIndex (ObservationId, EntityTypeId, EntityId)
        SELECT DISTINCT o.Id, g.EntityTypeId, g.EntityId
        FROM dbo.Observation o
        JOIN dbo.Location l       ON l.Id = o.LocationId
        JOIN dbo.LocationAreas la ON la.LocationId = l.Id
        JOIN #Omr g               ON g.AreaId = la.AreaId
        WHERE o.LocationId IS NOT NULL
          AND o.Id >= @CurrentId AND o.Id <= @BatchEnd
          AND NOT EXISTS (
                SELECT 1 FROM dbo.ObservationEntityIndex x
                WHERE x.ObservationId = o.Id
                  AND x.EntityTypeId  = g.EntityTypeId
                  AND x.EntityId      = g.EntityId)
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
    END

    SET @Total = @Total + @Rows;

    IF @DryRun = 0
        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'X46_Insert' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 2 innsett | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET @Msg = CONCAT('Steg 2 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ===========================================================================
-- STEG 3-6: fyll kolonnene paa de nye radene
--
-- Speiler seksjon C, D, E3 og E4 i BackfillAll, men avgrenset til #Typer.
-- Predikatene er de samme og er OPPFYLLBARE: de treffer bare rader der kilden
-- har en verdi indeksen mangler, saa raden matcher ikke lenger etter
-- oppdateringen. Uten det ville verifiseringen nederst aldri naa null.
--
-- Klyngenoekkelen er (ObservationId, EntityTypeId, EntityId), saa
-- ObservationId-intervallet gir et seek og EntityTypeId filtrerer paa andre
-- noekkelkolonne.
-- ===========================================================================

-- --- STEG 3: denormaliserte filterkolonner (som seksjon C) -----------------
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 3: denormaliserte filterkolonner ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'X46_FilterColumns'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    IF @DryRun = 1
        SELECT @Rows = COUNT(*)
        FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND idx.TaxonGroupId = 0;
    ELSE
    BEGIN
        UPDATE idx
        SET idx.TaxonGroupId                = o.TaxonGroupId,
            idx.CategoryId                  = o.CategoryId,
            idx.BasisOfRecordId             = o.BasisOfRecordId,
            idx.CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters,
            idx.DateTimeCollected           = o.DateTimeCollected,
            idx.HasMediaFiles = CASE WHEN EXISTS (
                SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id) THEN 1 ELSE 0 END,
            idx.RegistrationStatusId = CASE
                WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 6) THEN 3
                WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 5) THEN 2
                ELSE 1 END
        FROM dbo.ObservationEntityIndex idx
        INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
        INNER JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND idx.TaxonGroupId = 0
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
    END

    SET @Total = @Total + @Rows;

    IF @DryRun = 0
        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'X46_FilterColumns' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 3 kolonner | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET @Msg = CONCAT('Steg 3 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- --- STEG 4: taksonrangkolonner (som seksjon D) ----------------------------
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 4: taksonrangkolonner ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'X46_TaxonRanks'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    IF @DryRun = 1
        SELECT @Rows = COUNT(*)
        FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
        JOIN #Typer t                        ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND (   (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
               OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
               OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
               OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL));
    ELSE
    BEGIN
        UPDATE idx
        SET idx.SpeciesTaxonId = h.SpeciesTaxonId,
            idx.GenusTaxonId   = h.GenusTaxonId,
            idx.FamilyTaxonId  = h.FamilyTaxonId,
            idx.OrderTaxonId   = h.OrderTaxonId
        FROM dbo.ObservationEntityIndex idx
        INNER JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
        INNER JOIN #Typer t                        ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND (   (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
               OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
               OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
               OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL))
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
    END

    SET @Total = @Total + @Rows;

    IF @DryRun = 0
        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'X46_TaxonRanks' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 4 takson | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET @Msg = CONCAT('Steg 4 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- --- STEG 5: InstitutionOrgId / DatasetOrgId (som seksjon E3) --------------
--
-- Leser fra Observation, som seksjon E1 allerede har fylt. Er E1 ikke kjoert i
-- dette miljoeet, blir kolonnene staaende NULL her ogsaa - da er det E1 som er
-- problemet, ikke dette skriptet. Verifiseringen nederst viser det.
--
-- KOLONNENAVN: seksjon E3 i BackfillAll skriver til CollectionOrgId. Den
-- kolonnen heter DatasetOrgId etter migrasjon 20260901124537. BackfillAll
-- beholder det gamle navnet med vilje - paa et ferskt miljoe kjoerer den foer
-- omdoepingen - men dette skriptet kjoerer alltid mot en ferdigmigrert base og
-- maa bruke dagens navn. Kjoeres BackfillAll.sql manuelt mot produksjon i dag,
-- feiler den med «Invalid column name 'CollectionOrgId'».
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 5: organisasjonskolonner ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'X46_OrgColumns'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    IF @DryRun = 1
        SELECT @Rows = COUNT(*)
        FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND (   (idx.InstitutionOrgId IS NULL AND o.InstitutionOrgId IS NOT NULL)
               OR (idx.DatasetOrgId  IS NULL AND o.DatasetOrgId  IS NOT NULL));
    ELSE
    BEGIN
        UPDATE idx
        SET idx.InstitutionOrgId = o.InstitutionOrgId,
            idx.DatasetOrgId  = o.DatasetOrgId
        FROM dbo.ObservationEntityIndex idx
        INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
        INNER JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND (   (idx.InstitutionOrgId IS NULL AND o.InstitutionOrgId IS NOT NULL)
               OR (idx.DatasetOrgId  IS NULL AND o.DatasetOrgId  IS NOT NULL))
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
    END

    SET @Total = @Total + @Rows;

    IF @DryRun = 0
        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'X46_OrgColumns' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 5 org | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET @Msg = CONCAT('Steg 5 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- --- STEG 6: BehaviorId (som seksjon E4) -----------------------------------
--
-- BehaviorId er tinyint. Kilden ObservationBehaviors er verifisert 1:1 i
-- BackfillAll, saa castet er trygt. ~73 % av observasjonene har ingen atferd,
-- og NULL er da riktig sluttilstand - derfor joiner predikatet mot kilden i
-- stedet for aa lete etter NULL.
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 6: atferd ===', 0, 1) WITH NOWAIT;

SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
                            WHERE Section = 'X46_Behavior'), @MinId - 1) + 1;
SET @Total = 0;
SET @SectionStart = SYSUTCDATETIME();

IF @CurrentId > @MaxId
    RAISERROR('Allerede fullfoert - hopper over.', 0, 1) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    IF @DryRun = 1
        SELECT @Rows = COUNT(*)
        FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationBehaviors b ON b.ObservationId = idx.ObservationId
        JOIN #Typer t                   ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND idx.BehaviorId IS NULL;
    ELSE
    BEGIN
        UPDATE idx
        SET idx.BehaviorId = CAST(b.BehaviorId AS TINYINT)
        FROM dbo.ObservationEntityIndex idx
        INNER JOIN dbo.ObservationBehaviors b ON b.ObservationId = idx.ObservationId
        INNER JOIN #Typer t                   ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.ObservationId >= @CurrentId AND idx.ObservationId <= @BatchEnd
          AND idx.BehaviorId IS NULL
        OPTION (RECOMPILE);

        SET @Rows = @@ROWCOUNT;
    END

    SET @Total = @Total + @Rows;

    IF @DryRun = 0
        MERGE dbo.BackfillProgress AS t
        USING (SELECT 'X46_Behavior' AS Section, @BatchEnd AS LastCompletedId) AS s
            ON t.Section = s.Section
        WHEN MATCHED THEN UPDATE SET LastCompletedId = s.LastCompletedId, UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, s.LastCompletedId);

        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | 6 atferd | ',
                          FORMAT(@CurrentId, 'N0'), '-', FORMAT(@BatchEnd, 'N0'),
                          ' | ', FORMAT(@Rows, 'N0'),
                          ' | totalt ', FORMAT(@Total, 'N0'),
                          ' | ', DATEDIFF(SECOND, @SectionStart, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET @Msg = CONCAT('Steg 6 ferdig: ', FORMAT(@Total, 'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;


-- ===========================================================================
-- STEG 7: statistikk, columnstore og verifisering
-- ===========================================================================
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('=== STEG 7: etterarbeid ===', 0, 1) WITH NOWAIT;

-- Statistikk.
--
-- Bare indeksstatistikken oppdateres, ikke alt med FULLSCAN slik BackfillAll
-- gjoer. Det som endret seg er fordelingen paa EntityTypeId/EntityId, og den
-- ligger i IX_ObservationEntityIndex_EntityLookup. En full oppdatering av alle
-- kolonnestatistikker koster 10-20 minutter og er ikke noedvendig naar bare to
-- omraadetyper er roert.
IF @DryRun = 0 AND @Statistikk = 1
BEGIN
    RAISERROR('Oppdaterer statistikk...', 0, 1) WITH NOWAIT;
    UPDATE STATISTICS dbo.ObservationEntityIndex IX_ObservationEntityIndex_EntityLookup WITH FULLSCAN;
    RAISERROR('  IX_ObservationEntityIndex_EntityLookup ferdig.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Statistikk hoppet over.', 0, 1) WITH NOWAIT;

-- Columnstore.
--
-- Nye rader havner i deltastore. Indeksen er korrekt uansett, men umpakkede
-- rader leses i row mode og gir ikke batch-gevinsten. REORGANIZE komprimerer
-- dem uten et fullt gjenoppbygg.
IF @DryRun = 0 AND @Reorganiser = 1
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Komprimerer columnstore-deltastore...', 0, 1) WITH NOWAIT;
    ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex
        REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON);
    RAISERROR('  Ferdig.', 0, 1) WITH NOWAIT;
END

-- ---------------------------------------------------------------------------
-- Verifisering — avgrenset til #Typer.
--
-- Kontroll 1 er den BackfillAll mangler: finnes radene som burde finnes?
-- Alle kontrollene er oppfyllbare og naar null naar arbeidet er gjort.
-- ---------------------------------------------------------------------------
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('--- Verifisering (kun AreaTypeId i #Typer) ---', 0, 1) WITH NOWAIT;

DECLARE @Kontroller TABLE (Nr INT, Navn NVARCHAR(80), Gjenstaar BIGINT);

INSERT INTO @Kontroller (Nr, Navn, Gjenstaar)
SELECT 1, 'Rader som mangler i indeksen',
       (SELECT COUNT_BIG(*)
        FROM (SELECT DISTINCT o.Id AS ObservationId, g.EntityTypeId, g.EntityId
              FROM dbo.Observation o
              JOIN dbo.Location l       ON l.Id = o.LocationId
              JOIN dbo.LocationAreas la ON la.LocationId = l.Id
              JOIN #Omr g               ON g.AreaId = la.AreaId
              WHERE o.LocationId IS NOT NULL) AS kilde
        WHERE NOT EXISTS (
              SELECT 1 FROM dbo.ObservationEntityIndex x
              WHERE x.ObservationId = kilde.ObservationId
                AND x.EntityTypeId  = kilde.EntityTypeId
                AND x.EntityId      = kilde.EntityId))
UNION ALL
SELECT 2, 'Rader som ikke kan utledes av kilden',
       (SELECT COUNT_BIG(*)
        FROM dbo.ObservationEntityIndex idx
        JOIN #Typer t ON t.AreaTypeId = idx.EntityTypeId
        WHERE NOT EXISTS (
              SELECT 1
              FROM dbo.Observation o
              JOIN dbo.Location l       ON l.Id = o.LocationId
              JOIN dbo.LocationAreas la ON la.LocationId = l.Id
              JOIN #Omr g               ON g.AreaId = la.AreaId
              WHERE o.Id = idx.ObservationId
                AND g.EntityTypeId = idx.EntityTypeId
                AND g.EntityId     = idx.EntityId))
UNION ALL
SELECT 3, 'Denormaliserte filterkolonner',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.TaxonGroupId = 0)
UNION ALL
SELECT 4, 'Taksonrangkolonner',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = idx.ObservationId
        JOIN #Typer t                        ON t.AreaTypeId = idx.EntityTypeId
        WHERE (idx.SpeciesTaxonId IS NULL AND h.SpeciesTaxonId IS NOT NULL)
           OR (idx.GenusTaxonId   IS NULL AND h.GenusTaxonId   IS NOT NULL)
           OR (idx.FamilyTaxonId  IS NULL AND h.FamilyTaxonId  IS NOT NULL)
           OR (idx.OrderTaxonId   IS NULL AND h.OrderTaxonId   IS NOT NULL))
UNION ALL
SELECT 5, 'InstitutionOrgId/DatasetOrgId',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.Observation o ON o.Id = idx.ObservationId
        JOIN #Typer t          ON t.AreaTypeId = idx.EntityTypeId
        WHERE (idx.InstitutionOrgId IS NULL AND o.InstitutionOrgId IS NOT NULL)
           OR (idx.DatasetOrgId  IS NULL AND o.DatasetOrgId  IS NOT NULL))
UNION ALL
SELECT 6, 'BehaviorId',
       (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex idx
        JOIN dbo.ObservationBehaviors b ON b.ObservationId = idx.ObservationId
        JOIN #Typer t                   ON t.AreaTypeId = idx.EntityTypeId
        WHERE idx.BehaviorId IS NULL);

SELECT Nr, Navn, Gjenstaar,
       CASE WHEN Gjenstaar = 0 THEN 'OK' ELSE 'MANGLER' END AS Status
FROM @Kontroller ORDER BY Nr;

-- Radantall per område, til øyekontroll mot Area.ObservationCount.
RAISERROR('', 0, 1) WITH NOWAIT;
RAISERROR('--- Rader per omraade ---', 0, 1) WITH NOWAIT;

SELECT g.EntityTypeId, g.EntityId, g.Fid, g.Navn,
       ISNULL(x.Rader, 0) AS RaderIIndeks
FROM #Omr g
LEFT JOIN (SELECT idx.EntityTypeId, idx.EntityId, COUNT_BIG(*) AS Rader
           FROM dbo.ObservationEntityIndex idx
           JOIN #Typer t ON t.AreaTypeId = idx.EntityTypeId
           GROUP BY idx.EntityTypeId, idx.EntityId) AS x
       ON x.EntityTypeId = g.EntityTypeId AND x.EntityId = g.EntityId
ORDER BY g.EntityTypeId, ISNULL(x.Rader, 0) DESC;

DECLARE @Mangler BIGINT = (SELECT SUM(Gjenstaar) FROM @Kontroller);

SET @Msg = CONCAT('Total tid: ', DATEDIFF(MINUTE, @RunStart, SYSUTCDATETIME()), ' minutter.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

IF @DryRun = 1
    RAISERROR('DRY RUN - ingenting ble skrevet. Sett @DryRun = 0 for aa kjoere.', 0, 1) WITH NOWAIT;
ELSE IF @Mangler > 0
BEGIN
    SET @Msg = CONCAT('UFULLSTENDIG: ', FORMAT(@Mangler, 'N0'),
                      ' rader gjenstaar. Kjoer skriptet paa nytt - det fortsetter der det slapp.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - AreaTypeId 4 og 6 er komplette.', 0, 1) WITH NOWAIT;

DROP TABLE IF EXISTS #Omr;
DROP TABLE IF EXISTS #Typer;

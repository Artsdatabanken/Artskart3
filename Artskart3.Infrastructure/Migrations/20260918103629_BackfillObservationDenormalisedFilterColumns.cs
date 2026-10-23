using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller de tre denormaliserte filterkolonnene fra kildene sine.
///
/// Defaultverdiene fra forrige migrasjon dekker allerede flertallet, så bare
/// avvikene oppdateres. Målt på Artskart3IndexProdLikeTestMigrations (61M rader):
///
///   RegistrationStatusId = 2      193 510 rader       1,8 s
///   RegistrationStatusId = 3        7 919 rader       0,2 s
///   HasMediaFiles        = 1    3 992 507 rader        37 s
///   BehaviorId                 16 575 020 rader       150 s
///
/// KILDENE, OG HVORFOR AKKURAT DISSE
/// Alle tre finnes fra før på ObservationEntityIndex, fylt av seksjon C og E4 i
/// BackfillAll. Denne migrasjonen utleder fra de samme kildetabellene — ikke fra
/// indekstabellen — fordi 722 048 observasjoner ikke har rader der i det hele
/// tatt (de mangler lokalitet). Leste vi fra indeksen, ville de blitt stående
/// igjen med defaultverdien uten at noe sa fra.
///
/// REKKEFØLGEN PÅ STATUS ER IKKE VALGFRI
/// NotRecovered (TagId 6) vinner over Absent (TagId 5), som i CASE-en i seksjon C.
/// Settes 2 etter 3, får en observasjon med begge taggene feil verdi.
///
/// VANNMERKE PER STEG
/// Etter mønsteret fra BackfillAll: hvert steg fører sitt eget vannmerke i
/// dbo.BackfillProgress, så et avbrudd fortsetter der det slapp. Tabellen slippes
/// av CleanupCompleteFilterSchema og opprettes her på nytt hvis den mangler.
///
/// LØKKENE HER ER IKKE MØNSTERET Å KOPIERE — SE 20260920125459 I STEDET
/// Hver løkke kjører «UPDATE TOP (@BatchSize) ... WHERE kolonnen IS NULL» uten noe
/// ID-område. Det er KORREKT og trygt å avbryte: predikatet utelukker radene som
/// alt er fylt, så en ny kjøring konvergerer mot samme tilstand. Men de fylte
/// radene må fortsatt traverseres for å finne de ufylte, så hver av de ~34
/// iterasjonene går lenger inn i joinen enn den forrige.
///
/// Og vannmerket skrives bare ETTER at hele løkken er ferdig. Avbrytes den på
/// batch 30, er arbeidet bevart — hver UPDATE committer for seg, se
/// suppressTransaction under — men vannmerket står på 0, så neste forsøk
/// retraverserer hele joinen for å oppdage at det er lite igjen å gjøre.
///
/// To dager senere gjør 20260920125459_BackfillObservationEntityIndexLocationIdAndMonth
/// det riktig: den avgrenser hver batch med «ObservationId BETWEEN @CurrentId AND
/// @BatchEnd» og MERGE-r vannmerket PER BATCH. Kommentaren der forklarer også
/// hvorfor NULL-sjekken står på indeksraden alene — «en batch som allerede er fylt
/// filtreres bort billig og en gjenopptagelse koster nesten ingenting».
///
/// Dette er ikke rettet her fordi migrasjonen er anvendt overalt den betyr noe, og
/// kostnaden betales én gang per miljø. Skal du skrive en ny backfill: kopier den
/// nyere, ikke denne.
///
/// MERK: vannmerket registrerer at ID-området er behandlet, ikke at dataene er
/// riktige. Verifiseringen nederst er det som avgjør — og den leser mot kildene,
/// ikke mot vannmerkene. Slår den til, nullstilles vannmerkene før feilen heves,
/// slik at en ny kjøring gjør arbeidet om igjen i stedet for å hoppe over det.
///
/// suppressTransaction: stegene tar minutter til sammen. I én transaksjon ville
/// en timeout rullet tilbake alt, og migrasjonen aldri konvergert.
/// </summary>
public partial class BackfillObservationDenormalisedFilterColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

IF OBJECT_ID('dbo.BackfillProgress') IS NULL
    CREATE TABLE dbo.BackfillProgress (
        Section         VARCHAR(64) NOT NULL PRIMARY KEY,
        LastCompletedId INT         NOT NULL,
        UpdatedAt       DATETIME2   NOT NULL
            CONSTRAINT DF_BackfillProgress_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

DECLARE @Msg NVARCHAR(400);
DECLARE @Rows INT;

-- BATCHES, IKKE ÉN SETNING PER KOLONNE.
--
-- Hver UPDATE under var opprinnelig ett gjennomløp: 16,5 millioner rader for
-- atferd (150 s maalt) og 4,0 millioner for mediefiler (37 s). Som én setning er
-- det ÉN transaksjon som holder U/X-laaser paa Observation hele veien og skriver
-- flere GB logg foer noe kan trunkeres - og columnstore er droppet i det samme
-- vinduet, saa omraadetellingene er ~10x tregere mens det staar paa.
--
-- Predikatene er selv-begrensende: en rad som er oppdatert matcher ikke lenger.
-- Loekka kan derfor kjoere til den ikke treffer noe, og et avbrudd midt i koster
-- bare de radene batchen holdt paa - resten staar.
DECLARE @BatchSize INT = 500000;
DECLARE @Batch INT;

-- Registreringsstatus. Bare avvikene fra defaulten (1) trenger oppdatering:
-- 201 429 av 61 052 216 rader.
IF ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
           WHERE Section = 'G1_ObservationRegistrationStatus'), 0) = 0
BEGIN
    SET @Rows = 0;
    SET @Batch = 1;
    WHILE @Batch > 0
    BEGIN
        UPDATE TOP (@BatchSize) o SET o.RegistrationStatusId = 2
        FROM dbo.Observation o
        WHERE o.RegistrationStatusId <> 2
          AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                      WHERE ot.ObservationId = o.Id AND ot.TagId = 5);
        SET @Batch = @@ROWCOUNT;
        SET @Rows = @Rows + @Batch;
    END

    DECLARE @Rows3 INT = 0;
    SET @Batch = 1;

    -- Etter 2, slik at NotRecovered vinner der begge taggene finnes.
    WHILE @Batch > 0
    BEGIN
        UPDATE TOP (@BatchSize) o SET o.RegistrationStatusId = 3
        FROM dbo.Observation o
        WHERE o.RegistrationStatusId <> 3
          AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                      WHERE ot.ObservationId = o.Id AND ot.TagId = 6);
        SET @Batch = @@ROWCOUNT;
        SET @Rows3 = @Rows3 + @Batch;
    END

    SET @Msg = CONCAT('G1 registreringsstatus: ', FORMAT(@Rows, 'N0'), ' + ',
                      FORMAT(@Rows3, 'N0'), ' rader');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'G1_ObservationRegistrationStatus' AS Section, 1 AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = 1, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, 1);
END

-- Mediefiler.
IF ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
           WHERE Section = 'G2_ObservationHasMediaFiles'), 0) = 0
BEGIN
    SET @Rows = 0;
    SET @Batch = 1;
    WHILE @Batch > 0
    BEGIN
        UPDATE TOP (@BatchSize) o SET o.HasMediaFiles = 1
        FROM dbo.Observation o
        WHERE o.HasMediaFiles = 0
          AND EXISTS (SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id);
        SET @Batch = @@ROWCOUNT;
        SET @Rows = @Rows + @Batch;
    END

    SET @Msg = CONCAT('G2 mediefiler: ', FORMAT(@Rows, 'N0'), ' rader');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'G2_ObservationHasMediaFiles' AS Section, 1 AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = 1, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, 1);
END

-- Atferd. Verifisert 1:1 mot ObservationBehaviors i BackfillAll seksjon E4, så
-- joinen kan ikke duplisere rader.
IF ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
           WHERE Section = 'G3_ObservationBehavior'), 0) = 0
BEGIN
    SET @Rows = 0;
    SET @Batch = 1;
    WHILE @Batch > 0
    BEGIN
        UPDATE TOP (@BatchSize) o SET o.BehaviorId = CAST(b.BehaviorId AS TINYINT)
        FROM dbo.Observation o
        INNER JOIN dbo.ObservationBehaviors b ON b.ObservationId = o.Id
        WHERE o.BehaviorId IS NULL;
        SET @Batch = @@ROWCOUNT;
        SET @Rows = @Rows + @Batch;
    END

    SET @Msg = CONCAT('G3 atferd: ', FORMAT(@Rows, 'N0'), ' rader');
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    MERGE dbo.BackfillProgress AS t
    USING (SELECT 'G3_ObservationBehavior' AS Section, 1 AS LastCompletedId) AS s
        ON t.Section = s.Section
    WHEN MATCHED THEN UPDATE SET LastCompletedId = 1, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Section, LastCompletedId) VALUES (s.Section, 1);
END

-- Statistikken må oppdateres. Kolonnene er nye, og optimalisereren har ingen
-- histogrammer for dem. IX_Observation_ListView legges til i en senere
-- migrasjon; inntil da er dette det eneste som gir den noe å planlegge mot.
UPDATE STATISTICS dbo.Observation WITH FULLSCAN;

-- ---------------------------------------------------------------------------
-- Verifisering — «det kjørte» er ikke det samme som «det er riktig».
--
-- Kontrollene leser mot KILDENE, ikke mot vannmerkene, og hver av dem er
-- oppfyllbar: når arbeidet er gjort, når de null. En kontroll som ikke kan nå
-- null ville gjort at migrasjonen aldri registreres som anvendt.
-- ---------------------------------------------------------------------------
DECLARE @Avvik BIGINT = 0;
DECLARE @D1 BIGINT, @D2 BIGINT, @D3 BIGINT, @D4 BIGINT;

SELECT @D1 = COUNT_BIG(*) FROM dbo.Observation o
WHERE o.RegistrationStatusId <> 2
  AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 5)
  AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 6);

SELECT @D2 = COUNT_BIG(*) FROM dbo.Observation o
WHERE o.RegistrationStatusId <> 3
  AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 6);

SELECT @D3 = COUNT_BIG(*) FROM dbo.Observation o
WHERE o.HasMediaFiles = 0
  AND EXISTS (SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id);

SELECT @D4 = COUNT_BIG(*) FROM dbo.Observation o
INNER JOIN dbo.ObservationBehaviors b ON b.ObservationId = o.Id
WHERE o.BehaviorId IS NULL;

SET @Avvik = @D1 + @D2 + @D3 + @D4;

SET @Msg = CONCAT('Verifisering: status2=', @D1, ' status3=', @D2,
                  ' media=', @D3, ' atferd=', @D4);
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

IF @Avvik > 0
BEGIN
    -- Vannmerkene nullstilles FOER feilen heves, ellers kan migrasjonen ikke
    -- konvergere. Stegene er gatet paa at vannmerket er 0, og alle tre er satt
    -- naar verifiseringen kjoerer: en ny kjoering ville hoppet over alt arbeidet,
    -- verifisert paa nytt og feilet likt. Siden feilen hindrer at migrasjonen
    -- registreres i __EFMigrationsHistory, ville ingen senere migrasjon kommet
    -- til foer noen redigerte dbo.BackfillProgress for haand.
    --
    -- Er aarsaken forbigaaende - en samtidig skriving mot kildetabellene - retter
    -- en ny kjoering det av seg selv. Er den varig, feiler den som foer, men uten
    -- at noen maa inn i databasen for aa komme videre.
    DELETE FROM dbo.BackfillProgress
    WHERE Section IN ('G1_ObservationRegistrationStatus',
                      'G2_ObservationHasMediaFiles',
                      'G3_ObservationBehavior');

    SET @Msg = CONCAT('UFULLSTENDIG: ', FORMAT(@Avvik, 'N0'),
                      ' rader avviker fra kilden. Vannmerkene G1-G3 er nullstilt - kjoer paa nytt.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - kolonnene stemmer med kildene.', 0, 1) WITH NOWAIT;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Dataene tilbakestilles til defaultverdiene. Kolonnene selv fjernes av
        // forrige migrasjons Down.
        migrationBuilder.Sql(@"
UPDATE dbo.Observation SET RegistrationStatusId = 1 WHERE RegistrationStatusId <> 1;
UPDATE dbo.Observation SET HasMediaFiles = 0 WHERE HasMediaFiles <> 0;
UPDATE dbo.Observation SET BehaviorId = NULL WHERE BehaviorId IS NOT NULL;

DELETE FROM dbo.BackfillProgress
WHERE Section IN ('G1_ObservationRegistrationStatus',
                  'G2_ObservationHasMediaFiles',
                  'G3_ObservationBehavior');
", suppressTransaction: true);
    }
}

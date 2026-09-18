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
/// MERK: vannmerket registrerer at ID-området er behandlet, ikke at dataene er
/// riktige. Verifiseringen nederst er det som avgjør — og den leser mot kildene,
/// ikke mot vannmerkene.
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

-- Registreringsstatus. Bare avvikene fra defaulten (1) trenger oppdatering:
-- 201 429 av 61 052 216 rader.
IF ISNULL((SELECT LastCompletedId FROM dbo.BackfillProgress
           WHERE Section = 'G1_ObservationRegistrationStatus'), 0) = 0
BEGIN
    UPDATE o SET o.RegistrationStatusId = 2
    FROM dbo.Observation o
    WHERE o.RegistrationStatusId <> 2
      AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                  WHERE ot.ObservationId = o.Id AND ot.TagId = 5);
    SET @Rows = @@ROWCOUNT;

    -- Etter 2, slik at NotRecovered vinner der begge taggene finnes.
    UPDATE o SET o.RegistrationStatusId = 3
    FROM dbo.Observation o
    WHERE o.RegistrationStatusId <> 3
      AND EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                  WHERE ot.ObservationId = o.Id AND ot.TagId = 6);

    SET @Msg = CONCAT('G1 registreringsstatus: ', FORMAT(@Rows, 'N0'), ' + ',
                      FORMAT(@@ROWCOUNT, 'N0'), ' rader');
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
    UPDATE o SET o.HasMediaFiles = 1
    FROM dbo.Observation o
    WHERE o.HasMediaFiles = 0
      AND EXISTS (SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id);

    SET @Msg = CONCAT('G2 mediefiler: ', FORMAT(@@ROWCOUNT, 'N0'), ' rader');
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
    UPDATE o SET o.BehaviorId = CAST(b.BehaviorId AS TINYINT)
    FROM dbo.Observation o
    INNER JOIN dbo.ObservationBehaviors b ON b.ObservationId = o.Id
    WHERE o.BehaviorId IS NULL;

    SET @Msg = CONCAT('G3 atferd: ', FORMAT(@@ROWCOUNT, 'N0'), ' rader');
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
    SET @Msg = CONCAT('UFULLSTENDIG: ', FORMAT(@Avvik, 'N0'),
                      ' rader avviker fra kilden. Nullstill G1-G3 i dbo.BackfillProgress og kjoer paa nytt.');
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

-- ============================================================================
-- Backfill av denormaliserte kolonner på ObservationEntityIndex
-- Kjøres manuelt i SSMS etter at migrasjonen har lagt til kolonnene.
--
-- Kolonner som oppdateres:
--   TaxonGroupId, CategoryId, BasisOfRecordId, CoordinatePrecisionInMeters,
--   DateTimeCollected, HasMediaFiles, RegistrationStatusId
--
-- RegistrationStatusId-verdier:
--   1 = Funn (standard — ingen Absent/NotRecovered-tagger)
--   2 = Ikke funnet (TagId = 5 / Absent)
--   3 = Ikke gjenfunnet (TagId = 6 / NotRecovered)
--
-- TaxonGroupId = 0 brukes som markør for uoppdaterte rader.
-- Skriptet kan trygt kjøres flere ganger — det plukker opp der det slapp.
--
-- Bruker ID-range-basert batching i stedet for UPDATE TOP for jevn ytelse.
-- UPDATE TOP krever full tabellskanning for å finne uoppdaterte rader, som
-- blir tregere og tregere etter hvert som flere rader oppdateres.
-- ID-range gjør et index seek til riktig vindu hver gang.
-- ============================================================================

SET NOCOUNT ON;

-- RAISERROR med WITH NOWAIT brukes i stedet for PRINT fordi SSMS
-- bufrer PRINT-output og viser den først når spørringen er ferdig.
-- RAISERROR med severity 0 og WITH NOWAIT flusher meldingen umiddelbart
-- til Messages-fanen, slik at fremdrift kan følges i sanntid.

-- Deaktiver indekser under backfill for å unngå vedlikehold per rad
RAISERROR('Deaktiverer IX_ObservationEntityIndex_EntityLookup...', 0, 1) WITH NOWAIT;
ALTER INDEX IX_ObservationEntityIndex_EntityLookup ON dbo.ObservationEntityIndex DISABLE;
RAISERROR('Indeks deaktivert.', 0, 1) WITH NOWAIT;
RAISERROR('---', 0, 1) WITH NOWAIT;

DECLARE @BatchSize INT = 500000;
DECLARE @LastObsId INT = 0;
DECLARE @MaxObsId INT;
DECLARE @RowsUpdated INT;
DECLARE @TotalUpdated BIGINT = 0;
DECLARE @StartTime DATETIME2 = SYSUTCDATETIME();
DECLARE @Msg NVARCHAR(500);

SELECT @MaxObsId = MAX(ObservationId) FROM dbo.ObservationEntityIndex;

-- Sjekk hvor mange rader som gjenstår
DECLARE @Remaining BIGINT;
SELECT @Remaining = COUNT(*) FROM ObservationEntityIndex WITH (NOLOCK) WHERE TaxonGroupId = 0;
SET @Msg = CONCAT('Rader som gjenstaar: ', FORMAT(@Remaining, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Max ObservationId: ', FORMAT(@MaxObsId, 'N0'), ' | Estimerte batcher: ', CEILING(CAST(@MaxObsId AS FLOAT) / @BatchSize));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
RAISERROR('---', 0, 1) WITH NOWAIT;

WHILE @LastObsId < @MaxObsId
BEGIN
    UPDATE idx
    SET idx.TaxonGroupId = o.TaxonGroupId,
        idx.CategoryId = o.CategoryId,
        idx.BasisOfRecordId = o.BasisOfRecordId,
        idx.CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters,
        idx.DateTimeCollected = o.DateTimeCollected,
        idx.HasMediaFiles = CASE WHEN EXISTS (
            SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id
        ) THEN 1 ELSE 0 END,
        idx.RegistrationStatusId = CASE
            WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 6) THEN 3
            WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot WHERE ot.ObservationId = o.Id AND ot.TagId = 5) THEN 2
            ELSE 1
        END
    FROM dbo.ObservationEntityIndex idx
    INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
    WHERE idx.ObservationId > @LastObsId
      AND idx.ObservationId <= @LastObsId + @BatchSize
      AND idx.TaxonGroupId = 0;

    SET @RowsUpdated = @@ROWCOUNT;
    SET @TotalUpdated += @RowsUpdated;
    SET @LastObsId += @BatchSize;

    SET @Msg = CONCAT(
        FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | ',
        'Batch ferdig: ', FORMAT(@RowsUpdated, 'N0'), ' rader | ',
        'Totalt: ', FORMAT(@TotalUpdated, 'N0'), ' | ',
        'Fremdrift: ', FORMAT(CAST(@LastObsId AS FLOAT) / @MaxObsId * 100, 'N1'), '%%', ' | ',
        'Tid: ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), 's'
    );
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
END

RAISERROR('---', 0, 1) WITH NOWAIT;
SET @Msg = CONCAT('Backfill ferdig! Totalt oppdatert: ', FORMAT(@TotalUpdated, 'N0'), ' rader paa ', DATEDIFF(SECOND, @StartTime, SYSUTCDATETIME()), ' sekunder.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

-- Gjenoppbygg indeks
RAISERROR('Gjenoppbygger IX_ObservationEntityIndex_EntityLookup...', 0, 1) WITH NOWAIT;
ALTER INDEX IX_ObservationEntityIndex_EntityLookup ON dbo.ObservationEntityIndex REBUILD;
RAISERROR('Indeks gjenoppbygget!', 0, 1) WITH NOWAIT;

/*
    INVARIANT: hver kommunerad i ObservationEntityIndex foelges av fylkesraden for
    kommunens eget fylke.

    KUN LESING. Skriptet skriver ingenting og kan kjoeres naar som helst, ogsaa
    under trafikk.

    HVORFOR DEN FINNES
    AreaCountGeoJoinRules.CanSkipGeoJoin utelater geo-semijoinen naar
    utdataomraadene er kommuner i et valgt fylke. Det hviler paa denne invarianten:
    ankerraden er en kommunerad, og det er fylkesraden som tilfredsstiller
    predikatet. Maalt til 0 brudd over 357 kommuner paa prodlik base.

    Brytes den, blir omraadeantallene paa kartet FOR HOEYE, uten feilmelding.
    Stille gale tall er verre enn en stoppet deploy, saa bruddet meldes som
    severity 16 - en pipeline som kjoerer dette skriptet stopper paa det.

    Maa invarianten mykes opp, er tilbakefallet aa returnere alleErValgte alene i
    CanSkipGeoJoin - se kommentaren der.

    HVORFOR SOM EGET SKRIPT, OG IKKE BARE I BackfillAll
    Kontrollen stod opprinnelig til slutt i Scripts/BackfillAll.sql. Den er
    embeddet i migrasjonen BackfillAll, og en anvendt migrasjon kjoeres aldri
    igjen - i produksjon, der invarianten faktisk kan brytes av et manuelt skript,
    ble den altsaa aldri kontrollert. Den kjoerte bare paa ferske databaser, som er
    nettopp dem som ikke kan bryte den.

    KJOER DEN:
      - etter hvert skript som skriver til ObservationEntityIndex, f.eks.
        BackfillOceanAndSvalbardAreas.sql eller RebuildLocationAreasForAreaTypes.sql
      - fra deploy-pipelinen, ved siden av CheckAreaIndexDrift.sql
      - foer tellebufferne bygges: bygges de over en brutt invariant, stivner de
        gale tallene i AreaCountCacheLevel1
*/

SET NOCOUNT ON;

DECLARE @Msg NVARCHAR(400);

-- Kommunerader uten fylkesraden for sitt eget fylke. Ingen join mot Observation:
-- raden finnes i indeksen, og observasjonsraden tilfoeyer ingenting predikatet
-- bruker - den ville bare lagt et oppslag i en 61M-raders tabell til per rad.
DECLARE @HierarkiBrudd BIGINT = (
    SELECT COUNT_BIG(*)
    FROM dbo.ObservationEntityIndex k
    JOIN dbo.Area ka
      ON ka.AreaTypeId = 1
     AND ka.IsCurrent = 1
     AND ka.ParentFid IS NOT NULL
     AND TRY_CAST(REPLACE(ka.Fid, '_', '') AS INT) = k.EntityId
    WHERE k.EntityTypeId = 1
      AND NOT EXISTS (
            SELECT 1 FROM dbo.ObservationEntityIndex f
            WHERE f.ObservationId = k.ObservationId
              AND f.EntityTypeId = 2
              AND f.EntityId = TRY_CAST(REPLACE(ka.ParentFid, '_', '') AS INT)));

-- Hvilke kommuner det gjelder, til feilsoeking. Tom naar invarianten holder.
SELECT TOP (50)
       ka.Fid                AS KommuneFid,
       ka.Name               AS Kommune,
       ka.ParentFid          AS FylkeFid,
       COUNT_BIG(*)          AS RaderUtenFylkesrad
FROM dbo.ObservationEntityIndex k
JOIN dbo.Area ka
  ON ka.AreaTypeId = 1
 AND ka.IsCurrent = 1
 AND ka.ParentFid IS NOT NULL
 AND TRY_CAST(REPLACE(ka.Fid, '_', '') AS INT) = k.EntityId
WHERE k.EntityTypeId = 1
  AND NOT EXISTS (
        SELECT 1 FROM dbo.ObservationEntityIndex f
        WHERE f.ObservationId = k.ObservationId
          AND f.EntityTypeId = 2
          AND f.EntityId = TRY_CAST(REPLACE(ka.ParentFid, '_', '') AS INT))
GROUP BY ka.Fid, ka.Name, ka.ParentFid
ORDER BY COUNT_BIG(*) DESC;

IF @HierarkiBrudd > 0
BEGIN
    SET @Msg = CONCAT('INVARIANT BRUTT: ', FORMAT(@HierarkiBrudd, 'N0'),
                      ' kommunerader mangler fylkesraden for sitt eget fylke. ',
                      'Omraadeantallene vil bli for hoeye. Se AreaCountGeoJoinRules.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Invariant OK - alle kommunerader har fylkesrad.', 0, 1) WITH NOWAIT;

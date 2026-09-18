-- ============================================================================
-- CheckAreaIndexDrift — avstemmer ObservationEntityIndex mot Area.
--
-- KUN LESING. Ingen INSERT, UPDATE eller DELETE. Trygg å kjøre i produksjon,
-- men steg 1 skanner hele indekstabellen, så kjør den utenom rushtid.
--
-- HVORFOR
-- Seksjon A i BackfillAll setter inn områderader fra
-- Observation -> Location -> LocationAreas -> Area, og den SETTER BARE INN.
-- Det finnes ingen DELETE noe sted i skriptet. Endrer Area seg — nye Fid-er,
-- gamle satt til IsCurrent = 0, justerte grenser — blir de gamle radene
-- liggende igjen, og en observasjon telles i både gammelt og nytt område.
--
-- Ufiltrerte kart-tall kommer fra Area.ObservationCount og ser da riktige ut.
-- Filtrerte tall kommer fra denne tabellen og blir for høye. Det er derfor
-- avviket må måles og ikke antas.
--
-- OMFANG: alle områdetyper, altså alt unntatt EntityTypeId 101 (Institution).
-- AreaTypeId 6 (Svalbard/Bjørnøya/Jan Mayen) er med — INSERT-en i seksjon A
-- filtrerer ikke på AreaTypeId, selv om kommentaren der sier "1-4".
--
-- FID-KONVERTERING: speiler seksjon A nøyaktig.
--   AreaTypeId 3 (verneområde): fjerner prefikset 'Naturbase VV'
--   ellers:                     fjerner understrek ('15_2017' -> 152017)
-- TRY_CAST i stedet for CAST: her vil vi se de radene som ikke lar seg
-- konvertere, ikke avbryte spørringen på dem.
-- ============================================================================

SET NOCOUNT ON;

-- ---------------------------------------------------------------------------
-- Steg 1: samle indeksens distinkte områder med radantall.
--
-- Dette er den dyre delen — én aggregering over hele tabellen. Alt etterpå går
-- mot noen hundre rader. Batch mode via IX_OEI_Columnstore gjør den vanligvis
-- til et minutt eller to; uten columnstore faller den tilbake på
-- IX_ObservationEntityIndex_EntityLookup, som også er nøklet
-- (EntityTypeId, EntityId).
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS #IndexAreas;

SELECT EntityTypeId,
       EntityId,
       COUNT_BIG(*) AS Rader
INTO #IndexAreas
FROM dbo.ObservationEntityIndex
WHERE EntityTypeId <> 101          -- institusjonsrader er ikke områder
GROUP BY EntityTypeId, EntityId;

CREATE UNIQUE CLUSTERED INDEX PK_IndexAreas ON #IndexAreas (EntityTypeId, EntityId);

-- ---------------------------------------------------------------------------
-- Steg 2: de gjeldende områdene, oversatt til samme (type, id)-nøkkel.
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS #ValidAreas;

SELECT a.AreaTypeId AS EntityTypeId,
       TRY_CAST(CASE WHEN a.AreaTypeId = 3
                     THEN REPLACE(a.Fid, 'Naturbase VV', '')
                     ELSE REPLACE(a.Fid, '_', '') END AS INT) AS EntityId,
       MIN(a.Fid)   AS EksempelFid,
       MIN(a.Name)  AS EksempelNavn,
       COUNT(*)     AS AntallAreaRader
INTO #ValidAreas
FROM dbo.Area a
WHERE a.IsCurrent = 1
GROUP BY a.AreaTypeId,
         TRY_CAST(CASE WHEN a.AreaTypeId = 3
                       THEN REPLACE(a.Fid, 'Naturbase VV', '')
                       ELSE REPLACE(a.Fid, '_', '') END AS INT);

-- ---------------------------------------------------------------------------
-- RAPPORT 1: hovedtallet — hvor mange rader må slettes?
--
-- Rader i indeksen for områder som ikke lenger er gjeldende. Det er disse som
-- gir dobbelttelling i filtrerte områdetellinger.
-- ---------------------------------------------------------------------------
PRINT '=== 1. FORELDRELØSE RADER (må slettes) ===';

SELECT ISNULL(SUM(i.Rader), 0) AS ForeldreloeseRader,
       COUNT(*)                AS ForeldreloeseOmraader
FROM #IndexAreas i
LEFT JOIN #ValidAreas v
       ON v.EntityTypeId = i.EntityTypeId
      AND v.EntityId     = i.EntityId
WHERE v.EntityId IS NULL;

PRINT '';
PRINT '--- fordelt paa omraadetype ---';

SELECT i.EntityTypeId,
       CASE i.EntityTypeId
            WHEN 1 THEN 'Kommune'
            WHEN 2 THEN 'Fylke'
            WHEN 3 THEN 'Verneomraade'
            WHEN 4 THEN 'Havomraade'
            WHEN 6 THEN 'Svalbard/Bjoernoeya/JanMayen'
            ELSE CONCAT('Ukjent (', i.EntityTypeId, ')') END AS Type,
       COUNT(*)      AS Omraader,
       SUM(i.Rader)  AS Rader
FROM #IndexAreas i
LEFT JOIN #ValidAreas v
       ON v.EntityTypeId = i.EntityTypeId
      AND v.EntityId     = i.EntityId
WHERE v.EntityId IS NULL
GROUP BY i.EntityTypeId
ORDER BY Rader DESC;

PRINT '';
PRINT '--- de 30 stoerste enkeltomraadene ---';

SELECT TOP 30
       i.EntityTypeId,
       i.EntityId,
       i.Rader
FROM #IndexAreas i
LEFT JOIN #ValidAreas v
       ON v.EntityTypeId = i.EntityTypeId
      AND v.EntityId     = i.EntityId
WHERE v.EntityId IS NULL
ORDER BY i.Rader DESC;

-- ---------------------------------------------------------------------------
-- RAPPORT 2: gjeldende områder uten rader i indeksen.
--
-- Dette er (omtrent) det seksjon A vil sette inn etter Area-kopien. Tallet er
-- et undertall: et område som allerede har NOEN rader dukker ikke opp her,
-- selv om det mangler rader for nye observasjoner.
--
-- Er lista tom etter kopien, har A ingenting å gjøre — og da er problemet et
-- annet sted, mest sannsynlig i LocationAreas.
-- ---------------------------------------------------------------------------
PRINT '';
PRINT '=== 2. GJELDENDE OMRAADER UTEN RADER I INDEKSEN ===';

SELECT v.EntityTypeId,
       CASE v.EntityTypeId
            WHEN 1 THEN 'Kommune'
            WHEN 2 THEN 'Fylke'
            WHEN 3 THEN 'Verneomraade'
            WHEN 4 THEN 'Havomraade'
            WHEN 6 THEN 'Svalbard/Bjoernoeya/JanMayen'
            ELSE CONCAT('Ukjent (', v.EntityTypeId, ')') END AS Type,
       COUNT(*) AS OmraaderUtenRader
FROM #ValidAreas v
LEFT JOIN #IndexAreas i
       ON i.EntityTypeId = v.EntityTypeId
      AND i.EntityId     = v.EntityId
WHERE i.EntityId IS NULL
GROUP BY v.EntityTypeId
ORDER BY OmraaderUtenRader DESC;

PRINT '';
PRINT '--- de 30 foerste ---';

SELECT TOP 30
       v.EntityTypeId, v.EntityId, v.EksempelFid, v.EksempelNavn
FROM #ValidAreas v
LEFT JOIN #IndexAreas i
       ON i.EntityTypeId = v.EntityTypeId
      AND i.EntityId     = v.EntityId
WHERE i.EntityId IS NULL
ORDER BY v.EntityTypeId, v.EntityId;

-- ---------------------------------------------------------------------------
-- RAPPORT 3: kollisjoner i Fid-konverteringen.
--
-- To ulike Fid-er som havner på samme EntityId innenfor samme type ville blitt
-- slått sammen til ett område i indeksen — en stille dobbelttelling som verken
-- rapport 1 eller 2 fanger, fordi nøkkelen da finnes på begge sider.
--
-- Forventet resultat: tom.
-- ---------------------------------------------------------------------------
PRINT '';
PRINT '=== 3. FID-KOLLISJONER (forventet: tom) ===';

SELECT v.EntityTypeId, v.EntityId, v.AntallAreaRader
FROM #ValidAreas v
WHERE v.AntallAreaRader > 1
ORDER BY v.AntallAreaRader DESC;

-- ---------------------------------------------------------------------------
-- RAPPORT 4: Fid-er som ikke lar seg konvertere til int.
--
-- Seksjon A bruker CAST, ikke TRY_CAST. Finnes det slike rader blant de
-- gjeldende områdene, VIL backfillen feile — ikke gi feil tall, men avbryte.
--
-- Forventet resultat: tom.
-- ---------------------------------------------------------------------------
PRINT '';
PRINT '=== 4. FID-ER SOM IKKE ER NUMERISKE (forventet: tom) ===';

SELECT a.Id, a.AreaTypeId, a.Fid, a.Name
FROM dbo.Area a
WHERE a.IsCurrent = 1
  AND TRY_CAST(CASE WHEN a.AreaTypeId = 3
                    THEN REPLACE(a.Fid, 'Naturbase VV', '')
                    ELSE REPLACE(a.Fid, '_', '') END AS INT) IS NULL;

-- ---------------------------------------------------------------------------
-- RAPPORT 5: status for backfill-vannmerkene.
--
-- Står disse på MAX(Observation.Id), hopper BackfillAll over alle seksjonene
-- og gjør ingenting. A, C, D, E3 og E4 må nullstilles etter Area-kopien.
-- B, E1 og E2 skal IKKE nullstilles — de har ingenting med Area å gjøre, og
-- B er den dyreste seksjonen.
-- ---------------------------------------------------------------------------
PRINT '';
PRINT '=== 5. BACKFILL-VANNMERKER ===';

IF OBJECT_ID('dbo.BackfillProgress') IS NULL
    PRINT 'dbo.BackfillProgress finnes ikke - backfillen har aldri kjoert her.';
ELSE
    SELECT p.Section,
           p.LastCompletedId,
           p.UpdatedAt,
           (SELECT MAX(Id) FROM dbo.Observation) AS MaksObservationId,
           CASE WHEN p.Section IN ('A_EntityIndexRows', 'C_EntityIndexFilterColumns',
                                   'D_EntityIndexTaxonRanks', 'E3_EntityIndexOrgColumns',
                                   'E4_EntityIndexBehavior')
                THEN 'MAA NULLSTILLES' ELSE 'la staa' END AS EtterAreaKopi
    FROM dbo.BackfillProgress p
    ORDER BY p.Section;

DROP TABLE IF EXISTS #IndexAreas;
DROP TABLE IF EXISTS #ValidAreas;

-- ============================================================================
-- Artskart2NodeDatasets — tabellen som oversetter NodeIds i gamle Artskart 2-lenker.
--
-- Artskart 2 kunne filtrere på importkilde (Observation.NodeId), fra datasettsiden
-- og administrasjonssidene sine. Artskart 3 har ikke det filteret. Datasettfilteret
-- (DatasetOrgId) gir likevel nøyaktig samme utvalg for en kilde når alle funnene
-- har datasett og ingen annen kilde deler dem. Bare de kildene tas med; lenker med
-- andre kilder får en advarsel om at filteret ikke ble brukt.
--
-- LESER BARE. Skanner Observation én gang, siden NodeId ikke har noen indeks.
--
-- Resultatet er én JSON-verdi. Lagre den som
-- Artskart3.WebApp/src/assets/artskart2-nodes.json.
-- ============================================================================

WITH Par AS (
    SELECT NodeId, DatasetOrgId
    FROM dbo.Observation
    GROUP BY NodeId, DatasetOrgId
),
Utelatte AS (
    SELECT NodeId FROM Par WHERE DatasetOrgId IS NULL
    UNION
    SELECT p.NodeId
    FROM Par p
    JOIN Par annen ON annen.DatasetOrgId = p.DatasetOrgId AND annen.NodeId <> p.NodeId
),
Noder AS (
    SELECT NodeId,
           STRING_AGG(CAST(DatasetOrgId AS varchar(max)), ',') WITHIN GROUP (ORDER BY DatasetOrgId) AS Datasett
    FROM Par
    WHERE NodeId NOT IN (SELECT NodeId FROM Utelatte)
    GROUP BY NodeId
)
SELECT '{' + STRING_AGG(CAST('"' + CAST(NodeId AS varchar(12)) + '":[' + Datasett + ']' AS varchar(max)), ',')
       WITHIN GROUP (ORDER BY NodeId) + '}' AS Json
FROM Noder;

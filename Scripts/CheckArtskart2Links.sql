-- ============================================================================
-- CheckArtskart2Links — kontrollerer at gamle Artskart 2-lenker fortsatt kan
-- oversettes, typisk etter at databasen er bygd på nytt.
--
-- LESER BARE. Alle spørringene er små, bortsett fra taggkontrollene, som går
-- gjennom de filtrerte indeksene på taggkolonnene.
--
-- Hver spørring sier hva den forventer. Se også Scripts/README.md, som har hele
-- lista over hva som skal sjekkes, og Artskart3.WebApp/scripts/check-artskart2-links.mjs,
-- som sjekker det som trenger API-ene.
-- ============================================================================

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- 1. Bokmerkene (#bookmark/<guid>) leses fra Filter-tabellen, der Artskart 2
--    skrev GUID-en som første felt i JSON-en. Forventet: rundt 3 330 bokmerker,
--    og like mange med GUID-en først. Er tabellen tom eller borte, sier alle
--    bokmerkelenker at bokmerket ikke finnes lenger.
SELECT COUNT_BIG(*) AS Bokmerker,
       SUM(CASE WHEN SerializedFilter LIKE '{"Id":"%' THEN 1 ELSE 0 END) AS MedGuidFoerst,
       SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS MerketSlettet
FROM dbo.Filter;

-- 2. Filterkolonnene for sensitive, skjulte og annoterte funn fylles fra taggene
--    av migrasjoner, ikke av importen. Forventet: 0 i begge avvikskolonnene.
--    Avvik betyr at kolonnene ikke er fylt på nytt etter at dataene kom inn, og
--    da gir disse filtrene feil treff — i lenker og ellers.
SELECT Kolonne, TagId, KolonneUtenTagg, TaggUtenKolonne
FROM (VALUES
    ('Sensitive', 10,
        (SELECT COUNT_BIG(*) FROM dbo.Observation o
          WHERE o.Sensitive = 1
            AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTags t WHERE t.ObservationId = o.Id AND t.TagId = 10)),
        (SELECT COUNT_BIG(*) FROM dbo.ObservationTags t JOIN dbo.Observation o ON o.Id = t.ObservationId
          WHERE t.TagId = 10 AND o.Sensitive = 0)),
    ('HiddenByAnnotation', 7,
        (SELECT COUNT_BIG(*) FROM dbo.Observation o
          WHERE o.HiddenByAnnotation = 1
            AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTags t WHERE t.ObservationId = o.Id AND t.TagId = 7)),
        (SELECT COUNT_BIG(*) FROM dbo.ObservationTags t JOIN dbo.Observation o ON o.Id = t.ObservationId
          WHERE t.TagId = 7 AND o.HiddenByAnnotation = 0)),
    ('ChangedByAnnotation', 9,
        (SELECT COUNT_BIG(*) FROM dbo.Observation o
          WHERE o.ChangedByAnnotation = 1
            AND NOT EXISTS (SELECT 1 FROM dbo.ObservationTags t WHERE t.ObservationId = o.Id AND t.TagId = 9)),
        (SELECT COUNT_BIG(*) FROM dbo.ObservationTags t JOIN dbo.Observation o ON o.Id = t.ObservationId
          WHERE t.TagId = 9 AND o.ChangedByAnnotation = 0))
) AS Kontroll (Kolonne, TagId, KolonneUtenTagg, TaggUtenKolonne);

-- 3. Importkildene (NodeIds og datasettsiden i Artskart 2) må ha samme Id som i
--    Artskart 2 produksjon. Forventet navn:
--      1  Lichen herbarium, Oslo (O) UiO
--      34 Vascular Plants, Field notes, Agder naturmuseum (KMN) UiA
--    Avviker de, peker artskart2-nodes.json på feil kilder, også etter at den er
--    generert på nytt.
SELECT Id, Name FROM dbo.ImportState WHERE Id IN (1, 34) ORDER BY Id;

-- 4. Lista bruker ikke IsDeleted, så slettemerkede funn vises der. Forventet: 0.
SELECT COUNT_BIG(*) AS SlettemerkedeFunn FROM dbo.Observation WHERE IsDeleted = 1;

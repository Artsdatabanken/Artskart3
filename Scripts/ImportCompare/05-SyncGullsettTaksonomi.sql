/*
    05-SyncGullsettTaksonomi.sql — gjør gullbasens taksonomi eksakt lik Artskart 2s

    KJØRES MOT ARTSKART 3s GULLBASE, etter at taksonomispeilingen har kjørt
    (TaxonomyRefreshEndToEndTests mot RavenDB) og FØR importen.

        sqlcmd -S localhost -E -d Artskart3Index_GullsettMedium -b
          -i Scripts/ImportCompare/05-SyncGullsettTaksonomi.sql

    HVORFOR DETTE TRENGS. Speilingen legger til og oppdaterer, men sletter aldri — og
    det er riktig i drift, der observasjoner kan peke på et takson som er borte fra
    kilden. Men gullbasen ble opprinnelig fylt med et taksonomisnapshot fra mai 2026,
    og de radene står igjen etter speilingen: 27 987 taksoner og 906 navn som Artskart 2
    ikke har.

    OG DE KAN FORURENSE MÅLINGEN. 89 av restnavnene har en navnestreng som også finnes
    på en rad begge basene har. Navneoppslaget i importen slår opp på navnestrengen, så
    de 89 blir ekstra kandidater som Artskart 2 aldri så — og da måler riggen en
    forskjell porten ikke har.

    OBSERVASJONENE TØMMES FØRST. Det finnes fremmednøkler fra Observation til både Taxon
    og TaxonName, så restene kan ikke fjernes mens observasjonene står der. Importen
    tømmer dem likevel ved neste kjøring (ARTSKART_GULLSETT_RYDD=1), så det er ikke tap.

    ETTERPÅ: kjør importen, så resolveren bruker Artskart 2s taksonomi.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @fasit sysname = 'Artskart2Index_GullsettMedium';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__Gullsett')
BEGIN
    RAISERROR('Denne basen er ikke merket som gullbase (__Gullsett mangler). Avbryter.', 16, 1);
    RETURN;
END;

----------------------------------------------------------------------------------------
-- 1. Observasjonene. TRUNCATE der det går; DELETE der tabellen er referert.
----------------------------------------------------------------------------------------

TRUNCATE TABLE dbo.ObservationEntityIndex;
TRUNCATE TABLE dbo.ObservationTaxonHierarchy;
TRUNCATE TABLE dbo.SensitiveObservationData;
TRUNCATE TABLE dbo.ObservationLink;
TRUNCATE TABLE dbo.ObservationError;
TRUNCATE TABLE dbo.MediaFile;
TRUNCATE TABLE dbo.ObservationProject;
TRUNCATE TABLE dbo.ObservationBehaviors;
TRUNCATE TABLE dbo.ObservationTaxons;
TRUNCATE TABLE dbo.ObservationTags;
TRUNCATE TABLE dbo.LocationAreas;
TRUNCATE TABLE dbo.ObservationDetails;

DELETE FROM dbo.Observation;
DELETE FROM dbo.Location;

PRINT 'Observasjonene er tømt.';

----------------------------------------------------------------------------------------
-- 2. Taksonomirestene. Barna først — alle har fremmednøkkel til Taxon.
----------------------------------------------------------------------------------------

DELETE p
FROM dbo.TaxonPopularName p
WHERE p.TaxonId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Artskart2Index_GullsettMedium.dbo.Taxon a WHERE a.Id = p.TaxonId);

DELETE p
FROM dbo.TaxonProperty p
WHERE p.TaxonId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Artskart2Index_GullsettMedium.dbo.Taxon a WHERE a.Id = p.TaxonId);

DELETE n
FROM dbo.TaxonName n
WHERE NOT EXISTS (
    SELECT 1 FROM Artskart2Index_GullsettMedium.dbo.TaxonName a WHERE a.Id = n.Id);

DELETE t
FROM dbo.Taxon t
WHERE NOT EXISTS (
    SELECT 1 FROM Artskart2Index_GullsettMedium.dbo.Taxon a WHERE a.Id = t.Id);

----------------------------------------------------------------------------------------
-- 3. Kontroll. Skal vise 0 på alle fire.
----------------------------------------------------------------------------------------

SELECT
    TaksonBareIA3 = (SELECT COUNT_BIG(*) FROM (
        SELECT Id FROM dbo.Taxon
        EXCEPT SELECT Id FROM Artskart2Index_GullsettMedium.dbo.Taxon) x),
    TaksonBareIA2 = (SELECT COUNT_BIG(*) FROM (
        SELECT Id FROM Artskart2Index_GullsettMedium.dbo.Taxon
        EXCEPT SELECT Id FROM dbo.Taxon) x),
    NavnBareIA3 = (SELECT COUNT_BIG(*) FROM (
        SELECT Id FROM dbo.TaxonName
        EXCEPT SELECT Id FROM Artskart2Index_GullsettMedium.dbo.TaxonName) x),
    NavnBareIA2 = (SELECT COUNT_BIG(*) FROM (
        SELECT Id FROM Artskart2Index_GullsettMedium.dbo.TaxonName
        EXCEPT SELECT Id FROM dbo.TaxonName) x);

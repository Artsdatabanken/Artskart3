/* ============================================================================
   01-ScrubSensitiveCoordinates.sql
   Fjerner eksakte koordinater for sensitive arter fra en LOKAL KOPI av
   Artskart2Cache, slik at kopien kan brukes i paritetsriggen (plan 4.8.3).

   KJØR ALDRI MOT PRODUKSJON. Skriptet skriver destruktivt og har ingen anger.
   Ta backup av kopien før første kjøring.

   Skriptet er delt i fire steg med hver sin GO. Kjør ett om gangen og se på
   resultatet mellom stegene. Hvert steg gjentar sikkerhetssperren, slik at det
   er trygt å kjøre dem enkeltvis.

   ---------------------------------------------------------------------------
   HVORFOR DETTE IKKE ER EN VANLIG UPDATE

   Cachen har ingen koordinatkolonner. Alle Darwin Core-feltene ligger inne i
   DarwinCoreProxies.SerializedObject som GZip-komprimert UTF-16 JSON, skrevet av
   Sql2016CompressionHelper (GZipStream over Encoding.Unicode).

   SQL Servers DECOMPRESS/COMPRESS er kompatible med dette:
       CAST(DECOMPRESS(SerializedObject) AS NVARCHAR(MAX))   -- les
       COMPRESS(@json)                                        -- skriv

   Verifisert: rundturen bevarer teksten uendret.

   ---------------------------------------------------------------------------
   HVORFOR JSON_MODIFY(..., NULL) OG IKKE 0

   Serialiseringen bruker NullValueHandling.Ignore + DefaultValueHandling.Ignore,
   så felt med defaultverdi utelates helt fra JSON-en.

   JSON_MODIFY i lax-modus (standard) SLETTER nøkkelen når verdien er NULL. Det
   gir nøyaktig samme JSON-form som Artskart 2 selv ville skrevet for en post
   uten koordinat, og deserialiseringen får defaultverdier.

   Dette er avgjørende: SqlDarwinCoreProxy.ToDomainObject deserialiserer UTEN
   settings. Skrev vi "Latitude": null ville Newtonsoft kastet på å konvertere
   null til double. Sletting unngår det. Bruk derfor aldri 'strict $.felt'.

   ---------------------------------------------------------------------------
   HVORDAN SENSITIVE POSTER IDENTIFISERES

   Tre kriterier, forenet:

   1. NAVNEFORM. De 27 ScientificNameId-ene i SensitiveSpeciesRules slås opp i
      TaxonName for å finne TAKSONET, og deretter hentes ALLE navneformer for de
      taksonene. Taksonomien kjenner synonymene selv, så Alopex lagopus, Mergus
      albellus, Astur gentilis, Nyctea scandiaca, Philomachus pugnax og
      underartsformene kommer gratis — uten at noen må gjette slekter.
      Målt: 27 regel-IDer -> 27 taksoner -> 44 navneformer.

   2. SENSITIVT DATASETT. HarvestStates.IsSensitive = 1 for hele noden.

   3. ALLEREDE MASKERT. ProxyId-ene til observasjoner der Artskart 2 satte
      Tag 10. Fanger opp poster de to første kriteriene skulle bomme på.

   En tidligere versjon matchet på slektsnavn. Det var feil i begge retninger:
   det skrubbet 725 000 bergirisk-poster som ikke er sensitive, og manglet
   slektene Alopex, Astur og Mergus, som tre av artene faktisk forekommer under.

   ---------------------------------------------------------------------------
   MERK OM REGLENE I ARTSKART 2

   Tre av reglene har en ValidScientificName som ikke stemmer med taksonet IDen
   peker på: 3911 er Falco subbuteo (ikke Falco columbarius), 4205 er Emberiza
   rustica (ikke Linaria flavirostris), 156989 er Calidris pugnax (samme art som
   Philomachus pugnax, nytt navn).

   IDen er autoritativ — det er den ImportCheckSensitiveSpecies matcher på — så
   dette skriptet bruker IDene. Etikettene ligger i _ScrubRegel for sporbarhet.
============================================================================ */


/* ===========================================================================
   STEG 1 — Bygg arbeidstabellen

   Skanner DarwinCoreProxies, dekomprimerer og finner treff. Dette er den dyre
   delen: hele tabellen må dekomprimeres én gang. Batchet og gjenopptakbart —
   fremdriften ligger i _ScrubProgress, så kjøringen kan avbrytes og startes på
   nytt uten å miste arbeid.
=========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: kjor kun mot en lokal kopi av cachen.', 16, 1);
    SET NOEXEC ON;
END

-- >>> TILPASS: indeksbasen som har TaxonName/Observation. Navnelista hentes derfra. <<<
DECLARE @IndexDb SYSNAME = N'Artskart3IndexProdLikeTestMigrations';

IF OBJECT_ID('dbo._ScrubTargets') IS NULL
BEGIN
    CREATE TABLE dbo._ScrubTargets (
        ProxyRowId   INT           NOT NULL PRIMARY KEY,
        NodeId       INT           NOT NULL,
        Aarsak       VARCHAR(20)   NOT NULL,   -- SensitivNode | MaskertIIndex | Artsnavn
        Treff        NVARCHAR(200) NULL,
        ScrubbedAt   DATETIME2     NULL
    );
    CREATE INDEX IX_ScrubTargets_Todo ON dbo._ScrubTargets (ProxyRowId) WHERE ScrubbedAt IS NULL;
END

IF OBJECT_ID('dbo._ScrubProgress') IS NULL
    CREATE TABLE dbo._ScrubProgress (
        Seksjon         VARCHAR(40) NOT NULL PRIMARY KEY,
        LastCompletedId INT         NOT NULL,
        UpdatedAt       DATETIME2   NOT NULL DEFAULT SYSUTCDATETIME()
    );

/* --- Navnelista: hentes fra taksonomien, ikke gjettet ---------------------
   De 27 ScientificNameId-ene fra SensitiveSpeciesRules.GetRules() slås opp i
   TaxonName for å finne taksonet, og deretter hentes ALLE navneformer for de
   taksonene. Taksonomien kjenner synonymene selv — Alopex lagopus, Mergus
   albellus, Astur gentilis, Nyctea scandiaca, Philomachus pugnax og
   underartsformene kommer gratis.

   Målt i indeksbasen: 27 regel-IDer → 27 taksoner → 44 navneformer.
-------------------------------------------------------------------------- */
IF OBJECT_ID('dbo._ScrubRegel') IS NULL
    CREATE TABLE dbo._ScrubRegel (SciNameId INT PRIMARY KEY, Etikett NVARCHAR(60) NULL);

-- MERK: CREATE og INSERT er skilt med vilje, og vilkåret er «tom», ikke «finnes
-- ikke». Lå de i samme IF OBJECT_ID-blokk, ville en kjøring som brøt sammen
-- mellom de to setningene etterlate en tom tabell — og neste kjøring hoppet da
-- over hele blokka. Skrubben ville gått gjennom uten å treffe noe, uten å feile.
-- Samme grep er gjort for _ScrubNavn og _ScrubMaskert under.
IF NOT EXISTS (SELECT 1 FROM dbo._ScrubRegel)
BEGIN
    -- Nøyaktig ScientificNameId-verdiene i SensitiveSpeciesRules.GetRules().
    -- Etiketten er kodens ValidScientificName, tatt med for sporbarhet. MERK at
    -- tre av dem ikke stemmer med taksonet IDen peker på (3911, 4205, 156989) —
    -- IDen er autoritativ, det er den Artskart 2 matcher på.
    INSERT INTO dbo._ScrubRegel (SciNameId, Etikett) VALUES
        (48025,N'Canis lupus'),        (4614,N'Bubo scandiacus'),
        (3478,N'Anser erythropus'),    (48039,N'Ursus arctos'),
        (3910,N'Falco rusticolus'),    (4612,N'Bubo bubo'),
        (4626,N'Strix uralensis'),     (3859,N'Aquila chrysaetos'),
        (3918,N'Pandion haliaetus'),   (48072,N'Gulo gulo'),
        (48032,N'Vulpes lagopus'),     (48020,N'Lynx lynx'),
        (3907,N'Falco peregrinus'),    (3853,N'Accipiter gentilis'),
        (3878,N'Circus aeruginosus'),  (4624,N'Strix nebulosa'),
        (3880,N'Circus cyaneus'),      (3887,N'Haliaeetus albicilla'),
        (3911,N'Falco columbarius'),   (3521,N'Mergellus albellus'),
        (3899,N'Pernis apivorus'),     (4205,N'Linaria flavirostris'),
        (4200,N'Emberiza hortulana'),  (3756,N'Gallinago media'),
        (156989,N'Philomachus pugnax'),(3479,N'Anser fabalis'),
        (71197,N'Erioderma pedicellatum');
END

-- Navneformene hentes fra indeksbasen. Opprettes i cache-basen, så kollasjonen
-- følger cachen og sammenligningene under ikke får kollasjonskonflikt.
IF OBJECT_ID('dbo._ScrubNavn') IS NULL
    CREATE TABLE dbo._ScrubNavn (Navn NVARCHAR(200) PRIMARY KEY, TaxonId INT NOT NULL);

IF NOT EXISTS (SELECT 1 FROM dbo._ScrubNavn)
BEGIN
    DECLARE @sqlNavn NVARCHAR(MAX) = N'
    INSERT INTO dbo._ScrubNavn (Navn, TaxonId)
    SELECT n.Navn, n.TaxonId FROM (
        SELECT DISTINCT
               LTRIM(RTRIM(REPLACE(REPLACE(tn.ScientificName, N''<i>'', N''''), N''</i>'', N''''))) AS Navn,
               tn.TaxonId
        FROM ' + QUOTENAME(@IndexDb) + N'.dbo.TaxonName tn
        WHERE tn.TaxonId IN (
              SELECT tn2.TaxonId FROM ' + QUOTENAME(@IndexDb) + N'.dbo.TaxonName tn2
              JOIN dbo._ScrubRegel r ON r.SciNameId = tn2.Id)
    ) n
    WHERE n.Navn <> N'''';';

    EXEC sp_executesql @sqlNavn;

    -- Kontroll: alle 27 regler skal ha truffet et takson
    DECLARE @taksoner INT = (SELECT COUNT(DISTINCT TaxonId) FROM dbo._ScrubNavn);
    DECLARE @navneformer INT = (SELECT COUNT(*) FROM dbo._ScrubNavn);
    PRINT N'Navnelista: ' + CAST(@taksoner AS NVARCHAR(10)) + N' taksoner, '
        + CAST(@navneformer AS NVARCHAR(10)) + N' navneformer.';
    IF @taksoner < (SELECT COUNT(*) FROM dbo._ScrubRegel)
        RAISERROR(N'ADVARSEL: færre taksoner enn regler. Noen ScientificNameId finnes ikke i TaxonName.', 10, 1);
END

-- Belte og bukseseler: ProxyId-ene til observasjoner Artskart 2 faktisk maskerte.
-- Fanger opp poster navnelista skulle bomme på, f.eks. sensitive datasett.
IF OBJECT_ID('dbo._ScrubMaskert') IS NULL
    CREATE TABLE dbo._ScrubMaskert (ProxyId NVARCHAR(255) PRIMARY KEY);

-- Her kan tom liste være et riktig svar (ingen Tag 10 i indeksbasen), så «tom»
-- kan ikke brukes som vilkår. Et eksplisitt merke settes etter fullført fylling.
IF NOT EXISTS (SELECT 1 FROM dbo._ScrubProgress WHERE Seksjon = 'MaskertListeFylt')
BEGIN
    DELETE FROM dbo._ScrubMaskert;   -- rydder etter et eventuelt avbrutt forsøk

    DECLARE @sqlMask NVARCHAR(MAX) = N'
    INSERT INTO dbo._ScrubMaskert (ProxyId)
    SELECT DISTINCT o.ProxyId
    FROM ' + QUOTENAME(@IndexDb) + N'.dbo.Observation o
    JOIN ' + QUOTENAME(@IndexDb) + N'.dbo.ObservationTags ot
      ON ot.ObservationId = o.Id AND ot.TagId = 10
    WHERE o.ProxyId IS NOT NULL;';

    EXEC sp_executesql @sqlMask;
    -- PRINT tar bare skalaruttrykk, ikke subqueries (Msg 1046)
    DECLARE @antMaskert INT = (SELECT COUNT(*) FROM dbo._ScrubMaskert);
    PRINT N'Allerede maskerte i index: ' + CAST(@antMaskert AS NVARCHAR(20));

    -- Merket settes helt til slutt. Brøt fyllingen sammen, finnes merket ikke, og
    -- neste kjøring gjør et nytt forsøk i stedet for å tro at lista er komplett.
    INSERT INTO dbo._ScrubProgress (Seksjon, LastCompletedId) VALUES ('MaskertListeFylt', 1);
END

/* --- Sperre: uten navneliste treffer matchingen ingenting -------------------
   Siste sjanse til å stoppe. Kjøres løkka med tom _ScrubNavn, faller navne-
   kriteriet bort i det stille, _ScrubTargets blir nesten tom, og steg 3 melder
   at alt gikk bra — mens de eksakte koordinatene ligger igjen. Feilen ville
   ikke vises i noen logg. Derfor avbrytes kjøringen her.
-------------------------------------------------------------------------- */
DECLARE @nRegler INT = (SELECT COUNT(*) FROM dbo._ScrubRegel);
DECLARE @nNavn   INT = (SELECT COUNT(*) FROM dbo._ScrubNavn);
DECLARE @nTaxa   INT = (SELECT COUNT(DISTINCT TaxonId) FROM dbo._ScrubNavn);

PRINT N'Regelsett: ' + CAST(@nRegler AS NVARCHAR(10)) + N' regler, '
    + CAST(@nTaxa AS NVARCHAR(10)) + N' taksoner, '
    + CAST(@nNavn AS NVARCHAR(10)) + N' navneformer.  (malt: 27 / 27 / 44)';

IF @nRegler = 0 OR @nNavn = 0
BEGIN
    RAISERROR(N'STOPP: regelsett eller navneliste er tom. Sjekk at @IndexDb peker pa en base med TaxonName, kjor DROP TABLE dbo._ScrubNavn og start steg 1 pa nytt.', 16, 1);
    SET NOEXEC ON;
END

-- Færre taksoner enn regler betyr at noen regel-IDer ikke finnes i TaxonName.
-- Ikke en stopp — taksonomien kan ha endret seg — men det skal ses på.
IF @nTaxa < @nRegler
    PRINT N'ADVARSEL: ' + CAST(@nRegler - @nTaxa AS NVARCHAR(10))
        + N' regel-IDer fant ikke noe takson i TaxonName. Kontroller for du gar videre.';

DECLARE @BatchSize INT = 20000;
DECLARE @MinId INT, @MaxId INT, @CurrentId INT, @BatchEnd INT, @Rows INT, @Total INT = 0;

SELECT @MinId = MIN(Id), @MaxId = MAX(Id) FROM dbo.DarwinCoreProxies;
SELECT @CurrentId = ISNULL((SELECT LastCompletedId FROM dbo._ScrubProgress
                            WHERE Seksjon = 'A_FinnMaal'), @MinId - 1) + 1;

RAISERROR('=== STEG 1: finner maal, Id %d til %d ===', 0, 1, @CurrentId, @MaxId) WITH NOWAIT;

WHILE @CurrentId <= @MaxId
BEGIN
    SET @BatchEnd = @CurrentId + @BatchSize - 1;

    /* --- Hvorfor to temp-tabeller og ikke én CTE-kjede --------------------
       CTE-er materialiseres ikke i SQL Server, de inlines. Sto uttrykket som
       én kjede, ble DECOMPRESS(SerializedObject) regnet ut på nytt for HVER
       referanse til j nedover i kjeden — og SciName/Genus/Species refereres
       flere ganger til i WHERE. Målt på 20 000 rader:

           ren dekomprimering .............. 0,6 s
           CTE-kjeden ...................... 173 s      <- 300x dyrere
           materialisert, 5x JSON_VALUE .... 6,8 s
           materialisert, OPENJSON WITH .... 3,5 s      <- denne

       OPENJSON med skjema parser dokumentet én gang i stedet for fem. De tre
       variantene gir identisk resultat (kontrollert: samme treff, 0 avvik).
    ---------------------------------------------------------------------- */
    IF OBJECT_ID('tempdb..#raa') IS NOT NULL DROP TABLE #raa;

    SELECT p.Id, p.NodeId, p.ProxyId AS SqlProxyId,
           CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)) AS j
    INTO #raa
    FROM dbo.DarwinCoreProxies p
    WHERE p.Id BETWEEN @CurrentId AND @BatchEnd;

    -- OPENJSON kaster feil på ugyldig JSON i stedet for å gi tomt resultat, så
    -- de radene må ut før uttrekket. ISJSON her koster ett gjennomløp; gjort i
    -- en WHERE mot j ville den utløst en ny dekomprimering per rad.
    DELETE FROM #raa WHERE ISJSON(j) <> 1;

    IF OBJECT_ID('tempdb..#felt') IS NOT NULL DROP TABLE #felt;

    SELECT r.Id, r.NodeId, r.SqlProxyId,
           LTRIM(RTRIM(ISNULL(o.ScientificName, N''))) AS SciName,
           -- navn satt sammen fra Genus/Species/Subspecies, slik
           -- TaxonomyHelper.ConstructScientificNameFromOtherFields gjør det
           -- når ScientificName mangler
           LTRIM(RTRIM(ISNULL(o.Genus, N'') + N' ' + ISNULL(o.Species, N'')
                + CASE WHEN ISNULL(o.Subspecies, N'') <> N''
                       THEN N' ' + o.Subspecies ELSE N'' END)) AS BygdNavn,
           ISNULL(o.JsonProxyId, N'') AS JsonProxyId
    INTO #felt
    FROM #raa r
    CROSS APPLY OPENJSON(r.j) WITH (
        ScientificName NVARCHAR(400) '$.ScientificName',
        Genus          NVARCHAR(200) '$.Genus',
        Species        NVARCHAR(200) '$.Species',
        Subspecies     NVARCHAR(200) '$.Subspecies',
        JsonProxyId    NVARCHAR(255) '$.ProxyId'
    ) o;

    DROP TABLE #raa;   -- j er stor; slipp den før matchingen

    -- MERK: EXISTS, ikke LEFT JOIN, mot HarvestStates. Tabellen kan ha flere
    -- rader per node (bl.a. per PartitionYear), og en join multipliserer da
    -- radene — noe som gir PK-brudd på _ScrubTargets fordi samme ProxyRowId
    -- kommer flere ganger i samme INSERT. NOT EXISTS-sjekken hjelper ikke der,
    -- siden den ser tabellen slik den var før setningen.
    --
    -- GROUP BY er et ekstra vern: skulle noe annet begynne å multiplisere
    -- rader, kollapser det her i stedet for å felle kjøringen.
    --
    -- _ScrubMaskert sjekkes i to separate EXISTS, ikke ett med OR mellom to
    -- kolonner. Ett OR-uttrykk hindrer indekssøk på ProxyId-nøkkelen.
    INSERT INTO dbo._ScrubTargets (ProxyRowId, NodeId, Aarsak, Treff)
    SELECT f.Id, f.NodeId,
           MAX(CASE WHEN f.ErSensitivNode     = 1 THEN 'SensitivNode'
                    WHEN f.ErAlleredeMaskert  = 1 THEN 'MaskertIIndex'
                    ELSE 'Artsnavn' END),
           -- NULLIF/ISNULL-kjeden: er ScientificName tomt, kom treffet fra det
           -- sammensatte navnet, og da er det det som skal loggføres.
           MAX(LEFT(CASE WHEN f.ErSensitivNode = 1
                         THEN N'node ' + CAST(f.NodeId AS NVARCHAR(10))
                         ELSE ISNULL(NULLIF(f.SciName, N''), f.BygdNavn) END, 200))
    FROM (
        SELECT g.Id, g.NodeId, g.SciName, g.BygdNavn,
               CASE WHEN EXISTS (SELECT 1 FROM dbo.HarvestStates hs
                                 WHERE hs.Id = g.NodeId AND hs.IsSensitive = 1)
                    THEN 1 ELSE 0 END AS ErSensitivNode,
               CASE WHEN EXISTS (SELECT 1 FROM dbo._ScrubMaskert m WHERE m.ProxyId = g.SqlProxyId)
                      OR EXISTS (SELECT 1 FROM dbo._ScrubMaskert m WHERE m.ProxyId = g.JsonProxyId)
                    THEN 1 ELSE 0 END AS ErAlleredeMaskert
        FROM #felt g
    ) f
    WHERE ( f.ErSensitivNode    = 1        -- hele datasettet er merket sensitivt
         OR f.ErAlleredeMaskert = 1        -- Artskart 2 maskerte den faktisk
         -- navneformen er én av taksonomiens former for et sensitivt takson.
         -- LIKE-varianten fanger opp autorstreng: "Bubo bubo (Linnaeus, 1758)".
         OR EXISTS (SELECT 1 FROM dbo._ScrubNavn n
                    WHERE f.SciName  = n.Navn
                       OR f.BygdNavn = n.Navn
                       OR f.SciName  LIKE n.Navn + N' %'
                       OR f.BygdNavn LIKE n.Navn + N' %') )
      AND NOT EXISTS (SELECT 1 FROM dbo._ScrubTargets t WHERE t.ProxyRowId = f.Id)
    GROUP BY f.Id, f.NodeId;

    DROP TABLE #felt;

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    MERGE dbo._ScrubProgress AS tgt
    USING (SELECT 'A_FinnMaal' AS Seksjon, @BatchEnd AS Verdi) AS src
        ON tgt.Seksjon = src.Seksjon
    WHEN MATCHED THEN UPDATE SET LastCompletedId = src.Verdi, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (Seksjon, LastCompletedId) VALUES (src.Seksjon, src.Verdi);

    IF @Rows > 0
        RAISERROR('  Id %d-%d: %d treff (totalt %d)', 0, 1, @CurrentId, @BatchEnd, @Rows, @Total) WITH NOWAIT;

    SET @CurrentId = @BatchEnd + 1;
END

SET NOEXEC OFF;
GO


/* ===========================================================================
   STEG 2 — Tørrkjøring. Se på omfanget FØR du skrubber.
=========================================================================== */
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: feil database.', 16, 1);
    SET NOEXEC ON;
END

SELECT Aarsak, COUNT_BIG(*) AS Poster, COUNT(DISTINCT NodeId) AS Noder
FROM dbo._ScrubTargets GROUP BY Aarsak;

-- Hvilke navneformer traff? Skal kun inneholde former for de 27 sensitive
-- taksonene. Dukker det opp noe annet, er navnelista for vid.
SELECT TOP 40 Treff, COUNT_BIG(*) AS Poster
FROM dbo._ScrubTargets WHERE Aarsak = 'Artsnavn'
GROUP BY Treff ORDER BY COUNT_BIG(*) DESC;

-- Navneformer i lista som IKKE ga treff. Ikke nødvendigvis feil — cachen
-- inneholder ikke alle navneformer — men verdt et blikk.
SELECT n.Navn, n.TaxonId
FROM dbo._ScrubNavn n
WHERE NOT EXISTS (SELECT 1 FROM dbo._ScrubTargets t
                  WHERE t.Treff = n.Navn OR t.Treff LIKE n.Navn + N' %')
ORDER BY n.TaxonId, n.Navn;

SELECT (SELECT COUNT_BIG(*) FROM dbo._ScrubTargets)      AS TotaltIMaal,
       (SELECT COUNT_BIG(*) FROM dbo.DarwinCoreProxies)  AS TotaltICachen,
       (SELECT COUNT(*)     FROM dbo._ScrubNavn)         AS Navneformer,
       (SELECT COUNT(DISTINCT TaxonId) FROM dbo._ScrubNavn) AS Taksoner;

SET NOEXEC OFF;
GO


/* ===========================================================================
   STEG 3 — Skrubb

   Batchet og gjenopptakbart: hver post merkes i _ScrubTargets.ScrubbedAt.

   SLETTES — koordinat:
       Latitude, Longitude, UTMnord, UTMost, UTMsone, MGRSNotation,
       FootprintWkt, CoordinateUncertaintyInMeters, GeodeticDatum
   SLETTES — stedsidentifikasjon:
       Locality, LocationRemarks, County, StateProvince
   SLETTES — fritekst som erfaringsmessig inneholder stedsbeskrivelser:
       Notes, FieldNotes, EventRemarks, Habitat, Substrat, Okologi
   SLETTES — kan bære koordinater i andre former, eller peke til dem:
       UnknownFields (verbatimLatitude, decimalLatitude, footprintWKT,
       verbatimLocality, municipality …), DynamicProperties,
       AssociatedMedia, Multimedias,
       AssociatedReferences (en GBIF-occurrence-URL kan slås opp eksternt og
       gi koordinaten tilbake)

   BEHOLDES BEVISST:
       ScientificName/Genus/Species  arten er sensitiv, ikke navnet
       Country                       "Norge"/"Norway" — ingen posisjon, men
                                     ImportGeometry sjekker den mot en hviteliste
       LocationID                    ugjennomsiktig kilde-id, driver
                                     node-1010-nøkkelen som riggen tester
       CatalogNumber, koder, datoer  ingen posisjonsinformasjon
       Hash-kolonnen                 røres ikke; begge løsninger leser samme
                                     verdi, så endringsdeteksjonen er konsistent

   MERKNAD OM County/StateProvince (nå slettet):
       En kommune er ofte mindre enn maskeringsrutene på 4-50 km som reglene
       krever for disse artene, så de er i seg selv stedsinformasjon.

       Konsekvenser for riggens dekning, alle uproblematiske for pariteten
       siden begge løsninger leser den samme posten:
         - kommunesentroide-stien i ImportGeometry treffer ikke disse postene.
           De får uansett koordinat fra 03-skriptet, så de ville ikke gått den
           veien
         - Jan Mayen-sjekken (StateProvince = "jan mayen") utløses ikke
         - annotasjoner med fylkesfilter treffer dem ikke

       02-AssignDestination.sql er upåvirket: den leser den opprinnelige
       koordinaten, ikke disse feltene.
=========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: feil database.', 16, 1);
    SET NOEXEC ON;
END

-- Rekkefølgesperre: 02-AssignDestination.sql MÅ ha kjørt først. Den trenger de
-- opprinnelige koordinatene for å velge et fylke punktet ikke skal havne i, og
-- dette steget sletter dem. Kjøres de i feil rekkefølge, er informasjonen tapt
-- for godt og alle postene må plasseres i én fast region i stedet.
--
-- Sjekken må gå gjennom sp_executesql. SQL Server kompilerer hele batchen før
-- noe kjøres, så en direkte referanse til DestKommuneFid gir «Invalid column
-- name» — ikke den beskjeden vi vil gi — når 02 ikke har lagt til kolonnen ennå.
DECLARE @manglerDest INT = 0, @utenDest INT = 0;

IF COL_LENGTH('dbo._ScrubTargets','DestKommuneFid') IS NULL
    SET @manglerDest = 1;
ELSE
BEGIN
    EXEC sp_executesql
        N'SELECT @n = COUNT(*) FROM dbo._ScrubTargets WHERE DestKommuneFid IS NULL',
        N'@n INT OUTPUT', @n = @utenDest OUTPUT;
    IF @utenDest > 0 SET @manglerDest = 1;
END

IF @manglerDest = 1
BEGIN
    PRINT N'Poster uten destinasjon: ' + CAST(@utenDest AS NVARCHAR(20));
    RAISERROR(N'STOPP: kjor 02-AssignDestination.sql ferdig for skrubbingen. Den trenger koordinatene dette steget sletter.', 16, 1);
    SET NOEXEC ON;
END

IF OBJECT_ID('tempdb..#batch') IS NOT NULL DROP TABLE #batch;
CREATE TABLE #batch (ProxyRowId INT PRIMARY KEY);

DECLARE @Batch INT = 2000, @N INT = 1, @Sum INT = 0;

RAISERROR('=== STEG 3: skrubber ===', 0, 1) WITH NOWAIT;

WHILE @N > 0
BEGIN
    DELETE FROM #batch;

    INSERT INTO #batch (ProxyRowId)
    SELECT TOP (@Batch) ProxyRowId FROM dbo._ScrubTargets
    WHERE ScrubbedAt IS NULL ORDER BY ProxyRowId;

    SET @N = @@ROWCOUNT;
    IF @N = 0 BREAK;

    BEGIN TRAN;

    UPDATE p
    SET SerializedObject = COMPRESS(j23.v)
    FROM dbo.DarwinCoreProxies p
    JOIN #batch b ON b.ProxyRowId = p.Id
    CROSS APPLY (VALUES (CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)))) j0(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j0.v,  '$.Latitude',                      NULL))) j1(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j1.v,  '$.Longitude',                     NULL))) j2(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j2.v,  '$.UTMnord',                       NULL))) j3(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j3.v,  '$.UTMost',                        NULL))) j4(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j4.v,  '$.UTMsone',                       NULL))) j5(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j5.v,  '$.MGRSNotation',                  NULL))) j6(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j6.v,  '$.FootprintWkt',                  NULL))) j7(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j7.v,  '$.CoordinateUncertaintyInMeters', NULL))) j8(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j8.v,  '$.GeodeticDatum',                 NULL))) j9(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j9.v,  '$.Locality',                      NULL))) j10(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j10.v, '$.LocationRemarks',               NULL))) j11(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j11.v, '$.Notes',                         NULL))) j12(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j12.v, '$.FieldNotes',                    NULL))) j13(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j13.v, '$.EventRemarks',                  NULL))) j14(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j14.v, '$.Habitat',                       NULL))) j15(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j15.v, '$.Substrat',                      NULL))) j16(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j16.v, '$.Okologi',                       NULL))) j17(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j17.v, '$.UnknownFields',                 NULL))) j18(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j18.v, '$.DynamicProperties',             NULL))) j19(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j19.v, '$.AssociatedReferences',          NULL))) j20(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j20.v, '$.County',                        NULL))) j21(v)
    CROSS APPLY (VALUES (JSON_MODIFY(j21.v, '$.StateProvince',                 NULL))) j22(v)
    CROSS APPLY (VALUES (JSON_MODIFY(
                         JSON_MODIFY(j22.v, '$.AssociatedMedia',               NULL),
                                            '$.Multimedias',                   NULL))) j23(v);

    UPDATE t SET ScrubbedAt = SYSUTCDATETIME()
    FROM dbo._ScrubTargets t JOIN #batch b ON b.ProxyRowId = t.ProxyRowId;

    COMMIT;

    SET @Sum = @Sum + @N;
    RAISERROR('  skrubbet %d (totalt %d)', 0, 1, @N, @Sum) WITH NOWAIT;
END

DROP TABLE #batch;
SET NOEXEC OFF;
GO


/* ===========================================================================
   STEG 4 — Hendelsestabellen og verifisering

   DarwinCoreProxyEvents.Snapshot inneholder en komplett komprimert JSON-kopi av
   posten for hver Created/Changed/Deleted-hendelse — inkludert koordinatene.
   Importen leser den ikke (alle referanser i Artskart 2 er skriving), så den
   tømmes i sin helhet.
=========================================================================== */
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE '%Cache%' OR DB_NAME() LIKE '%Prod%'
BEGIN
    PRINT N'Gjeldende base: ' + DB_NAME();
    RAISERROR(N'STOPP: feil database.', 16, 1);
    SET NOEXEC ON;
END

TRUNCATE TABLE dbo.DarwinCoreProxyEvents;

;WITH sjekk AS (
    SELECT p.Id, CAST(DECOMPRESS(p.SerializedObject) AS NVARCHAR(MAX)) AS j
    FROM dbo.DarwinCoreProxies p
    JOIN dbo._ScrubTargets t ON t.ProxyRowId = p.Id
)
SELECT
    SUM(CASE WHEN JSON_VALUE(j,'$.Latitude')      IS NOT NULL THEN 1 ELSE 0 END) AS Latitude,
    SUM(CASE WHEN JSON_VALUE(j,'$.Longitude')     IS NOT NULL THEN 1 ELSE 0 END) AS Longitude,
    SUM(CASE WHEN JSON_VALUE(j,'$.UTMnord')       IS NOT NULL THEN 1 ELSE 0 END) AS UTMnord,
    SUM(CASE WHEN JSON_VALUE(j,'$.UTMost')        IS NOT NULL THEN 1 ELSE 0 END) AS UTMost,
    SUM(CASE WHEN JSON_VALUE(j,'$.MGRSNotation')  IS NOT NULL THEN 1 ELSE 0 END) AS MGRS,
    SUM(CASE WHEN JSON_VALUE(j,'$.FootprintWkt')  IS NOT NULL THEN 1 ELSE 0 END) AS Footprint,
    SUM(CASE WHEN JSON_VALUE(j,'$.Locality')      IS NOT NULL THEN 1 ELSE 0 END) AS Locality,
    SUM(CASE WHEN JSON_VALUE(j,'$.County')        IS NOT NULL THEN 1 ELSE 0 END) AS County,
    SUM(CASE WHEN JSON_VALUE(j,'$.StateProvince') IS NOT NULL THEN 1 ELSE 0 END) AS StateProvince,
    SUM(CASE WHEN JSON_VALUE(j,'$.AssociatedReferences') IS NOT NULL THEN 1 ELSE 0 END) AS AssocRefs,
    SUM(CASE WHEN JSON_QUERY(j,'$.UnknownFields') IS NOT NULL THEN 1 ELSE 0 END) AS UnknownFields,
    SUM(CASE WHEN ISJSON(j) = 0 THEN 1 ELSE 0 END)                               AS UgyldigJson,
    COUNT_BIG(*)                                                                 AS Kontrollerte
FROM sjekk;

-- Alle kolonner over skal være 0. Er de det, er cachen trygg å bruke i riggen.
-- _ScrubTargets beholdes: neste skript genererer tilfeldige geometrier for
-- nøyaktig disse postene.

SET NOEXEC OFF;
GO

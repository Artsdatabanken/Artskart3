/*
    04-SammenlignGullsett.sql — paritetsmålingen mot Artskart 2 (plan 4.8.3)

    Sammenligner Artskart 3s gullbase med Artskart 2s fasit og skriver resultatet til
    ImportCompare.dbo.Avvik. Skriptet er idempotent: det oppretter registeret om det
    mangler, og hver kjøring blir en ny rad i dbo.Kjoring.

    KALLES MED SQLCMD-VARIABLER. Basenavnene kan ikke være T-SQL-parametre — en
    databasereferanse må stå som literal — så substitusjonen skjer i sqlcmd, før
    parsing. Alternativet var dynamisk SQL over hele skriptet, og det ville gjort
    hver spørring uleselig:

      sqlcmd -S localhost -E -d ImportCompare -b
        -v Sett="lite-uten-39" -v Noder="43;135;194"
        -v A2Base="Artskart2Index_GullsettLite" -v A3Base="Artskart3Index_GullsettLite"
        -i Scripts/ImportCompare/04-SammenlignGullsett.sql

    FORUTSETNINGER
      - Artskart 2s fasit for settet er bygget.
      - Artskart 3s side er kjørt av ImportGullsettTests mot gullbasen.
      - Begge baser ligger på samme instans.

    NØKLENE ER FORRETNINGSNØKLER, IKKE SURROGATER
      Observation matches på ProxyId, Location på LookupId, og områdekoblinger på
      LookupId + Area.Fid. Id-kolonnene er identiteter og kan ikke matche på tvers.

    HVA SOM MED VILJE IKKE SAMMENLIGNES
      - Observation.Id, LocationId, DateTimeRecordImported: surrogater og kjøretidspunkt.
      - Area.Id: de to basene har ulike områdesnapshot.
      - Geometriens SRID: Artskart 2 skriver 32633, Artskart 3 skriver 25833, og det er
        den vedtatte ommerkingen (plan 4.12.3). Geometrien sammenlignes som WKB.
*/

SET NOCOUNT ON;

-- Nodene i settet, fra -v Noder. SEMIKOLON, IKKE KOMMA: sqlcmd leser komma i -v som
-- skilletegn mellom variabler, og "43,135,194" blir avvist som ugyldig argument.
--
-- Er node 39 med, vil 742 poster avvike pa maskeringen: Artskart 2 maskerte dem, og
-- Artskart 3 har ikke fase 8.
DECLARE @noder TABLE (NodeId int PRIMARY KEY);
INSERT @noder SELECT CAST(value AS int) FROM STRING_SPLIT('$(Noder)', ';');

DECLARE @nodetekst nvarchar(200) = (SELECT STRING_AGG(CAST(NodeId AS varchar(10)), ',') FROM @noder);

----------------------------------------------------------------------------------------
-- Registeret
----------------------------------------------------------------------------------------

IF OBJECT_ID('dbo.Kjoring') IS NULL
BEGIN
    CREATE TABLE dbo.Kjoring
    (
        Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Kjoring PRIMARY KEY,
        Gullsett   nvarchar(50)   NOT NULL,
        Noder      nvarchar(200)  NOT NULL,
        A2Base     nvarchar(128)  NOT NULL,
        A3Base     nvarchar(128)  NOT NULL,
        Tidspunkt  datetime2(0)   NOT NULL CONSTRAINT DF_Kjoring_Tidspunkt DEFAULT SYSUTCDATETIME(),
        Merknad    nvarchar(1000) NULL
    );
END;

IF OBJECT_ID('dbo.Avvik') IS NULL
BEGIN
    CREATE TABLE dbo.Avvik
    (
        KjoringId  int            NOT NULL,
        Omrade     nvarchar(60)   NOT NULL,
        Maal       nvarchar(120)  NOT NULL,
        Antall     bigint         NOT NULL,
        Av         bigint         NULL,
        Forklart   bit            NOT NULL,
        Forklaring nvarchar(400)  NULL,
        CONSTRAINT PK_Avvik PRIMARY KEY (KjoringId, Omrade, Maal),
        CONSTRAINT FK_Avvik_Kjoring FOREIGN KEY (KjoringId) REFERENCES dbo.Kjoring (Id)
    );
END;

INSERT dbo.Kjoring (Gullsett, Noder, A2Base, A3Base, Merknad)
VALUES ('$(Sett)', @nodetekst, '$(A2Base)', '$(A3Base)',
        'Maskeringen (fase 8) og annotasjonene (fase 10) finnes ikke i Artskart 3.');

DECLARE @kjoring int = SCOPE_IDENTITY();

----------------------------------------------------------------------------------------
-- 1. Observasjoner: antall og nøkkelsett
----------------------------------------------------------------------------------------

CREATE TABLE #a2obs (ProxyId nvarchar(200) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY, NodeId int NOT NULL);
CREATE TABLE #a3obs (ProxyId nvarchar(200) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY, NodeId int NOT NULL);

INSERT #a2obs SELECT ProxyId COLLATE DATABASE_DEFAULT, NodeId
FROM $(A2Base).dbo.Observation WHERE NodeId IN (SELECT NodeId FROM @noder);

INSERT #a3obs SELECT ProxyId COLLATE DATABASE_DEFAULT, NodeId
FROM $(A3Base).dbo.Observation WHERE NodeId IN (SELECT NodeId FROM @noder);

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation', 'Bare i Artskart 2',
       (SELECT COUNT_BIG(*) FROM (SELECT ProxyId FROM #a2obs EXCEPT SELECT ProxyId FROM #a3obs) x),
       (SELECT COUNT_BIG(*) FROM #a2obs), 0, NULL
UNION ALL
SELECT @kjoring, 'Observation', 'Bare i Artskart 3',
       (SELECT COUNT_BIG(*) FROM (SELECT ProxyId FROM #a3obs EXCEPT SELECT ProxyId FROM #a2obs) x),
       (SELECT COUNT_BIG(*) FROM #a3obs), 0, NULL;

----------------------------------------------------------------------------------------
-- 2. Observasjoner: felt for felt, delt på om posten er annotert
--
-- ANNOTERTE POSTER ER FORKLART. Annotasjonstjenesten er fase 10 og finnes ikke i
-- Artskart 3, så hver kolonne en annotasjon kan overskrive vil avvike. Artskart 2s
-- HasAnnotations er merket som sann der det har skjedd, og det er delingen under.
----------------------------------------------------------------------------------------

CREATE TABLE #par
(
    ProxyId nvarchar(200) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    NodeId int NOT NULL,
    -- Annotert er Artskart 2s HasAnnotations. Blokkert er Tag 7, som annotasjons-
    -- tjenesten ogsa setter, men UTEN a merke posten med HasAnnotations. Begge er
    -- fase 10, og begge ma med for at delingen skal bli riktig.
    Annotert bit NOT NULL,
    Blokkert bit NOT NULL,
    -- Maskert er Tag 10. Artskart 2 grovner da koordinaten til en rutenettcelle og
    -- setter presisjonen til halve diagonalen (malt: 2 828 for 4 km, 35 355 for 50 km),
    -- og den grovner datoen. Fase 8 finnes ikke i Artskart 3, som skriver kildens
    -- eksakte verdier. Uten denne delingen framstar det som koordinatfeil.
    Maskert bit NOT NULL,
    -- Settes etter innsettingen: ulik navne-Id, men samme navnestreng. Krever en join
    -- mot begge basenes TaxonName, og hoerer derfor ikke i INSERT-en.
    SammeNavnestreng bit NOT NULL DEFAULT 0,
    a_East int NULL, b_East int NULL,
    a_North int NULL, b_North int NULL,
    a_Lat float NULL, b_Lat float NULL,
    a_Lon float NULL, b_Lon float NULL,
    a_Presisjon int NULL, b_Presisjon int NULL,
    a_Takson int NULL, b_Takson int NULL,
    a_Taksongruppe int NULL, b_Taksongruppe int NULL,
    a_Navneid int NULL, b_Navneid int NULL,
    a_Kategori int NULL, b_Kategori int NULL,
    a_Funntype int NULL, b_Funntype int NULL,
    a_Hash int NULL, b_Hash int NULL,
    a_Katalognr nvarchar(200) COLLATE DATABASE_DEFAULT NULL, b_Katalognr nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    a_Occurrence nvarchar(400) COLLATE DATABASE_DEFAULT NULL, b_Occurrence nvarchar(400) COLLATE DATABASE_DEFAULT NULL,
    a_Samlet datetime2(7) NULL, b_Samlet datetime2(7) NULL,
    a_Bestemt datetime2(7) NULL, b_Bestemt datetime2(7) NULL,
    a_Endret datetime2(7) NULL, b_Endret datetime2(7) NULL,
    a_Aar int NULL, b_Aar int NULL,
    a_Maaned int NULL, b_Maaned int NULL,
    a_Feil bit NULL, b_Feil bit NULL,
    a_Kvalitet int NULL, b_Kvalitet int NULL,
    a_Motor int NULL, b_Motor int NULL,
    a_Annotert bit NULL, b_Annotert bit NULL
);

INSERT #par
SELECT a.ProxyId COLLATE DATABASE_DEFAULT, a.NodeId, a.HasAnnotations,
       CASE WHEN EXISTS (SELECT 1 FROM $(A2Base).dbo.ObservationTags t
                         WHERE t.ObservationId = a.Id AND t.TagId = 7) THEN 1 ELSE 0 END,
       CASE WHEN EXISTS (SELECT 1 FROM $(A2Base).dbo.ObservationTags t
                         WHERE t.ObservationId = a.Id AND t.TagId = 10) THEN 1 ELSE 0 END,
       0,
       a.East, b.East, a.North, b.North, a.Latitude, b.Latitude, a.Longitude, b.Longitude,
       a.CoordinatePrecisionInMeters, b.CoordinatePrecisionInMeters,
       a.TaxonId, b.TaxonId, a.TaxonGroupId, b.TaxonGroupId,
       a.MatchedScientificNameId, b.MatchedScientificNameId,
       a.CategoryId, b.CategoryId, a.BasisOfRecordId, b.BasisOfRecordId,
       a.HashCode, b.HashCode,
       a.CatalogNumber COLLATE DATABASE_DEFAULT, b.CatalogNumber COLLATE DATABASE_DEFAULT,
       a.OccurenceId COLLATE DATABASE_DEFAULT, b.OccurrenceId COLLATE DATABASE_DEFAULT,
       a.DateTimeCollected, b.DateTimeCollected, a.DatetimeIdentified, b.DatetimeIdentified,
       a.DateLastModified, b.DateLastModified, a.YearCollected, b.YearCollected,
       a.MonthCollected, b.MonthCollected, a.HasErrors, b.HasErrors,
       a.ObservationQualityTypeId, b.ObservationQualityTypeId,
       a.ProcessEngineId, b.ProcessEngineId, a.HasAnnotations, b.HasAnnotations
FROM $(A2Base).dbo.Observation a
JOIN $(A3Base).dbo.Observation b
  ON b.ProxyId COLLATE DATABASE_DEFAULT = a.ProxyId COLLATE DATABASE_DEFAULT
WHERE a.NodeId IN (SELECT NodeId FROM @noder);

DECLARE @felles bigint = (SELECT COUNT_BIG(*) FROM #par);

-- Ulikhet med NULL regnet som en verdi: ISNULL-sammenligning på hver kolonne.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation', m.Maal, m.Antall, @felles, m.Forklart, m.Forklaring
FROM (
    SELECT 'East' AS Maal,
           SUM(CASE WHEN ISNULL(a_East,-2147483648) <> ISNULL(b_East,-2147483648) THEN 1 ELSE 0 END) AS Antall,
           CAST(1 AS bit) AS Forklart,
           'ProjNet 2.1 mot 1.2, inntil 1 m ost for ~26 grader ost. Plan 7.2 punkt 11.' AS Forklaring FROM #par
    UNION ALL SELECT 'North', SUM(CASE WHEN ISNULL(a_North,-2147483648) <> ISNULL(b_North,-2147483648) THEN 1 ELSE 0 END), 1,
           'ProjNet 2.1 mot 1.2. Plan 7.2 punkt 11.' FROM #par
    UNION ALL SELECT 'Latitude', SUM(CASE WHEN ISNULL(a_Lat,-999) <> ISNULL(b_Lat,-999) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'Longitude', SUM(CASE WHEN ISNULL(a_Lon,-999) <> ISNULL(b_Lon,-999) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'CoordinatePrecisionInMeters', SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'TaxonId', SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'TaxonGroupId', SUM(CASE WHEN ISNULL(a_Taksongruppe,-1) <> ISNULL(b_Taksongruppe,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'MatchedScientificNameId', SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'CategoryId', SUM(CASE WHEN ISNULL(a_Kategori,-1) <> ISNULL(b_Kategori,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'BasisOfRecordId', SUM(CASE WHEN ISNULL(a_Funntype,-1) <> ISNULL(b_Funntype,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'HashCode', SUM(CASE WHEN ISNULL(a_Hash,-1) <> ISNULL(b_Hash,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'CatalogNumber', SUM(CASE WHEN ISNULL(a_Katalognr,'') <> ISNULL(b_Katalognr,'') THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'OccurenceId -> OccurrenceId', SUM(CASE WHEN ISNULL(a_Occurrence,'') <> ISNULL(b_Occurrence,'') THEN 1 ELSE 0 END), 0,
           'Skrivefeilen i Artskart 2s kolonnenavn er rettet i Artskart 3.' FROM #par
    UNION ALL SELECT 'DateTimeCollected', SUM(CASE WHEN ISNULL(a_Samlet,'1753-01-01') <> ISNULL(b_Samlet,'1753-01-01') THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'DatetimeIdentified', SUM(CASE WHEN ISNULL(a_Bestemt,'1753-01-01') <> ISNULL(b_Bestemt,'1753-01-01') THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'DateLastModified', SUM(CASE WHEN ISNULL(a_Endret,'1753-01-01') <> ISNULL(b_Endret,'1753-01-01') THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'YearCollected', SUM(CASE WHEN ISNULL(a_Aar,-1) <> ISNULL(b_Aar,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'MonthCollected', SUM(CASE WHEN ISNULL(a_Maaned,-1) <> ISNULL(b_Maaned,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'HasErrors', SUM(CASE WHEN ISNULL(a_Feil,0) <> ISNULL(b_Feil,0) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'ObservationQualityTypeId', SUM(CASE WHEN ISNULL(a_Kvalitet,-1) <> ISNULL(b_Kvalitet,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'ProcessEngineId', SUM(CASE WHEN ISNULL(a_Motor,-1) <> ISNULL(b_Motor,-1) THEN 1 ELSE 0 END), 0, NULL FROM #par
    UNION ALL SELECT 'HasAnnotations', SUM(CASE WHEN ISNULL(a_Annotert,0) <> ISNULL(b_Annotert,0) THEN 1 ELSE 0 END), 1,
           'Annotasjoner er fase 10 og finnes ikke i Artskart 3.' FROM #par
) m;

-- Maskeringen (fase 8) er sin egen forklaring, og den treffer koordinatkolonnene.
-- Målt på node 39: alle 742 maskerte poster avviker på lat, lon, presisjon og øst/nord,
-- og alle datoavvikene er maskerte poster.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation maskert', m.Maal, m.Antall, m.Totalt, 1,
       'Avvik pa poster Artskart 2 har maskert (Tag 10). Fase 8 mangler i Artskart 3.'
FROM (
    SELECT 'Latitude' AS Maal,
           SUM(CASE WHEN ISNULL(a_Lat,-999) <> ISNULL(b_Lat,-999) AND Maskert = 1 THEN 1 ELSE 0 END) AS Antall,
           SUM(CASE WHEN ISNULL(a_Lat,-999) <> ISNULL(b_Lat,-999) THEN 1 ELSE 0 END) AS Totalt FROM #par
    UNION ALL SELECT 'Longitude',
           SUM(CASE WHEN ISNULL(a_Lon,-999) <> ISNULL(b_Lon,-999) AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Lon,-999) <> ISNULL(b_Lon,-999) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'CoordinatePrecisionInMeters',
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'DateTimeCollected',
           SUM(CASE WHEN ISNULL(a_Samlet,'1753-01-01') <> ISNULL(b_Samlet,'1753-01-01') AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Samlet,'1753-01-01') <> ISNULL(b_Samlet,'1753-01-01') THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'DatetimeIdentified',
           SUM(CASE WHEN ISNULL(a_Bestemt,'1753-01-01') <> ISNULL(b_Bestemt,'1753-01-01') AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Bestemt,'1753-01-01') <> ISNULL(b_Bestemt,'1753-01-01') THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'East',
           SUM(CASE WHEN ISNULL(a_East,-2147483648) <> ISNULL(b_East,-2147483648) AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_East,-2147483648) <> ISNULL(b_East,-2147483648) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'North',
           SUM(CASE WHEN ISNULL(a_North,-2147483648) <> ISNULL(b_North,-2147483648) AND Maskert = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_North,-2147483648) <> ISNULL(b_North,-2147483648) THEN 1 ELSE 0 END) FROM #par
) m;

-- Hvor mye av avviket som ligger på annoterte poster. Er de to tallene like, er
-- kolonnen fullt ut forklart av at fase 10 mangler.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation annotert/blokkert', m.Maal, m.Antall, m.Totalt, 1,
       'Avvik pa poster Artskart 2 har annotert. Fase 10 mangler i Artskart 3.'
FROM (
    SELECT 'TaxonId' AS Maal,
           SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END) AS Antall,
           SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) THEN 1 ELSE 0 END) AS Totalt FROM #par
    UNION ALL SELECT 'MatchedScientificNameId',
           SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'CategoryId',
           SUM(CASE WHEN ISNULL(a_Kategori,-1) <> ISNULL(b_Kategori,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Kategori,-1) <> ISNULL(b_Kategori,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'CoordinatePrecisionInMeters',
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'HashCode',
           SUM(CASE WHEN ISNULL(a_Hash,-1) <> ISNULL(b_Hash,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Hash,-1) <> ISNULL(b_Hash,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'ObservationQualityTypeId',
           SUM(CASE WHEN ISNULL(a_Kvalitet,-1) <> ISNULL(b_Kvalitet,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Kvalitet,-1) <> ISNULL(b_Kvalitet,-1) THEN 1 ELSE 0 END) FROM #par
    UNION ALL SELECT 'East',
           SUM(CASE WHEN ISNULL(a_East,-1) <> ISNULL(b_East,-1) AND (Annotert = 1 OR Blokkert = 1) THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_East,-1) <> ISNULL(b_East,-1) THEN 1 ELSE 0 END) FROM #par
) m;

-- TAKSONOMIEN I DE TO BASENE ER IKKE SAMME SNAPSHOT. Artskart 2s fasit har 152 818
-- taksoner, Artskart 3s referansedata 180 087, og et navn kan ha fått et nytt takson
-- mellom dem. Der navneoppslaget ga SAMME MatchedScientificNameId men ulik TaxonId, har
-- importen gjort det samme i begge — forskjellen ligger i referansedataene, ikke i
-- porten. Det er en egen forklaring enn annotasjonene, og den må måles, ikke antas.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation referansedata', 'TaxonId ulik, men samme MatchedScientificNameId',
       SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1)
                     AND ISNULL(a_Navneid,-1) = ISNULL(b_Navneid,-1)
                     AND Annotert = 0 AND Blokkert = 0
                THEN 1 ELSE 0 END),
       SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) THEN 1 ELSE 0 END), 1,
       'Ulikt taksonomisnapshot i de to basene. Navneoppslaget er likt, sa porten gjorde det samme.'
FROM #par;

-- OG SPEILVENDINGEN: ulik navne-Id, men SAMME navnestreng.
--
-- ID-ENE ER IKKE SURROGATER. Verken `TaxonName.Id` eller `Taxon.Id` er IDENTITY i noen
-- av basene — de er tildelt av taksonomikilden. Ulik Id betyr derfor ulik *navnerad*,
-- ikke en renummerering, og taksonomien kan være omstrukturert mellom snapshotene.
--
-- Målt for «Anthozoa»: Artskart 2 matcher navn 299928 (akseptert) på takson 227680 med
-- rang subphylum. Artskart 3 matcher navn 339 (akseptert) på takson 339 med rang class.
-- I Artskart 2 finnes navn 339 fortsatt, men som SYNONYM mot 227680. Navnet er altså
-- flyttet til et nytt takson på en annen rang, og har fått en ny akseptert navnerad.
--
-- Begge oppslagene velger den aksepterte raden for navnestrengen de får, så regelen er
-- den samme — men observasjonene havner på taksoner med ulik rang, og dermed i ulike
-- taksonfiltre. Det er referansedata, ikke porten, men det er ikke kosmetikk.
--
-- Navnestrengen er lagret med kursivmarkering i begge basene, og den sammenlignes
-- ordrett — det er samme kolonne og samme kilde, så markeringen er lik.
UPDATE p
SET SammeNavnestreng = 1
FROM #par p
JOIN $(A2Base).dbo.TaxonName an ON an.Id = p.a_Navneid
JOIN $(A3Base).dbo.TaxonName bn ON bn.Id = p.b_Navneid
WHERE ISNULL(p.a_Navneid,-1) <> ISNULL(p.b_Navneid,-1)
  AND an.ScientificName COLLATE DATABASE_DEFAULT = bn.ScientificName COLLATE DATABASE_DEFAULT;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation referansedata', 'MatchedScientificNameId ulik, men samme navnestreng',
       SUM(CASE WHEN SammeNavnestreng = 1 THEN 1 ELSE 0 END),
       SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) THEN 1 ELSE 0 END), 1,
       'Samme navnestreng, men ulik navnerad i kildens taksonomi. Taksonet bak kan ha annen rang.'
FROM #par;

-- ÉN SAMLET TELLING, IKKE EN SUM AV BØTTENE. En post kan være både maskert og annotert,
-- og da ville tre separate tall summert seg til mer enn avviket og «forklart» mer enn
-- det faktisk finnes. Her er forklaringen ett predikat per post, så hver post telles én
-- gang uansett hvor mange grunner den har.
--
-- Bøttene over står igjen fordi de sier HVILKEN forklaring som dominerer. Denne raden
-- sier bare om det er noe igjen å undersøke.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Observation forklart', m.Maal, m.Antall, m.Totalt, 1,
       'Poster som er maskerte (fase 8), annoterte/blokkerte (fase 10) eller rammet av taksonomisnapshotet.'
FROM (
    SELECT 'Latitude' AS Maal,
           SUM(CASE WHEN ISNULL(a_Lat,-999) <> ISNULL(b_Lat,-999) AND Forklart = 1 THEN 1 ELSE 0 END) AS Antall,
           SUM(CASE WHEN ISNULL(a_Lat,-999) <> ISNULL(b_Lat,-999) THEN 1 ELSE 0 END) AS Totalt
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'Longitude',
           SUM(CASE WHEN ISNULL(a_Lon,-999) <> ISNULL(b_Lon,-999) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Lon,-999) <> ISNULL(b_Lon,-999) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'CoordinatePrecisionInMeters',
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Presisjon,-1) <> ISNULL(b_Presisjon,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'DateTimeCollected',
           SUM(CASE WHEN ISNULL(a_Samlet,'1753-01-01') <> ISNULL(b_Samlet,'1753-01-01') AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Samlet,'1753-01-01') <> ISNULL(b_Samlet,'1753-01-01') THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'DatetimeIdentified',
           SUM(CASE WHEN ISNULL(a_Bestemt,'1753-01-01') <> ISNULL(b_Bestemt,'1753-01-01') AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Bestemt,'1753-01-01') <> ISNULL(b_Bestemt,'1753-01-01') THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'CategoryId',
           SUM(CASE WHEN ISNULL(a_Kategori,-1) <> ISNULL(b_Kategori,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Kategori,-1) <> ISNULL(b_Kategori,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'HashCode',
           SUM(CASE WHEN ISNULL(a_Hash,-1) <> ISNULL(b_Hash,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Hash,-1) <> ISNULL(b_Hash,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'ObservationQualityTypeId',
           SUM(CASE WHEN ISNULL(a_Kvalitet,-1) <> ISNULL(b_Kvalitet,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Kvalitet,-1) <> ISNULL(b_Kvalitet,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    -- Taksonkolonnene har en forklaring mer: samme navneoppslag, nytt takson.
    UNION ALL SELECT 'TaxonId',
           SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1
                                   OR ISNULL(a_Navneid,-1) = ISNULL(b_Navneid,-1) 
                                   OR SammeNavnestreng = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
    UNION ALL SELECT 'MatchedScientificNameId',
           SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) AND Forklart = 1 THEN 1 ELSE 0 END),
           SUM(CASE WHEN ISNULL(a_Navneid,-1) <> ISNULL(b_Navneid,-1) THEN 1 ELSE 0 END)
    FROM (SELECT *, CAST(CASE WHEN Annotert = 1 OR Blokkert = 1 OR Maskert = 1
                                   OR SammeNavnestreng = 1 THEN 1 ELSE 0 END AS bit) Forklart FROM #par) p
) m;

-- MÅLINGEN AVGJØR, IKKE EN HARDKODET LISTE. Er hele avviket i en kolonne dekket av den
-- samlede tellingen, er kolonnen forklart — og det skal rapporten si av seg selv, ellers
-- må den tolkes for hånd hver gang.
UPDATE a
SET Forklart = 1,
    Forklaring = 'Hele avviket ligger pa poster som er maskerte, annoterte/blokkerte eller rammet av taksonomisnapshotet.'
FROM dbo.Avvik a
JOIN dbo.Avvik samlet
  ON samlet.KjoringId = a.KjoringId AND samlet.Omrade = 'Observation forklart' AND samlet.Maal = a.Maal
WHERE a.KjoringId = @kjoring AND a.Omrade = 'Observation'
  AND a.Antall > 0 AND samlet.Antall = a.Antall;

----------------------------------------------------------------------------------------
-- 3. Lokaliteter
--
-- GEOMETRIEN SAMMENLIGNES SOM WKB, ikke med STEquals. STEquals returnerer NULL når
-- SRID-ene er ulike, og de ER ulike med vilje (32633 mot 25833, plan 4.12.3). WKB
-- bærer ikke SRID, så det er den sammenligningen som faktisk måler formen.
----------------------------------------------------------------------------------------

CREATE TABLE #locpar
(
    LookupId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    WkbUlik bit NOT NULL,
    A3Gyldig bit NOT NULL,
    A3GyldigEtterMakeValidLikA2 bit NOT NULL,
    FeltUlik bit NOT NULL,
    A3ErPunktA2ErOmriss bit NOT NULL
);

-- TRE PASS, IKKE ETT. STGeometryType og MakeValid kaster på en ugyldig instans, og en
-- CASE-vakt i en SELECT hjelper ikke: SQL Server garanterer ingen kortslutning for
-- UDT-kall, så hele INSERT-en ryker på én dårlig rad. En UPDATE evaluerer derimot SET
-- bare for radene WHERE slipper gjennom, så filteret virker der.
--
-- Det er også det billige: medium har 266 356 lokaliteter, og MakeValid på alle ville
-- vært arbeid for ingenting. Her kalles den bare på de ugyldige.
INSERT #locpar (LookupId, WkbUlik, A3Gyldig, A3GyldigEtterMakeValidLikA2, FeltUlik, A3ErPunktA2ErOmriss)
SELECT a.LookupId COLLATE DATABASE_DEFAULT,
       CASE WHEN a.Geometry.STAsBinary() = b.Geometry.STAsBinary() THEN 0 ELSE 1 END,
       b.Geometry.STIsValid(),
       0,
       CASE WHEN a.East = b.East AND a.North = b.North AND a.Latitude = b.Latitude
                 AND a.Longitude = b.Longitude
                 AND ISNULL(a.CoordinatePrecision,-1) = ISNULL(b.CoordinatePrecision,-1)
                 AND ISNULL(a.Locality,'') COLLATE DATABASE_DEFAULT = ISNULL(b.Locality,'') COLLATE DATABASE_DEFAULT
                 AND ISNULL(a.LocationId,'') COLLATE DATABASE_DEFAULT = ISNULL(b.LocationId,'') COLLATE DATABASE_DEFAULT
            THEN 0 ELSE 1 END,
       0
FROM $(A2Base).dbo.Location a
JOIN $(A3Base).dbo.Location b
  ON b.LookupId COLLATE DATABASE_DEFAULT = a.LookupId COLLATE DATABASE_DEFAULT
WHERE a.NodeId IN (SELECT NodeId FROM @noder);

-- Lagrer Artskart 2 den reparerte geometrien? Bare for de ugyldige — og MakeValid er
-- malt til a vaere byte-identisk no-op pa en gyldig geometri, sa svaret er ikke
-- avhengig av at den utelates ellers.
UPDATE p
SET A3GyldigEtterMakeValidLikA2 = 1
FROM #locpar p
JOIN $(A2Base).dbo.Location a ON a.LookupId COLLATE DATABASE_DEFAULT = p.LookupId
JOIN $(A3Base).dbo.Location b ON b.LookupId COLLATE DATABASE_DEFAULT = p.LookupId
WHERE p.A3Gyldig = 0 AND b.Geometry.MakeValid().STAsBinary() = a.Geometry.STAsBinary();

-- Omrisset falt bort: Artskart 3 endte med et punkt der Artskart 2 har et polygon. Da
-- avviste TryTolk omrisset, og lokaliteten ble stedfestet av koordinaten alene. En
-- ugyldig geometri er aldri et punkt, sa A3Gyldig = 1 er en trygg og tilstrekkelig vakt.
UPDATE p
SET A3ErPunktA2ErOmriss = 1
FROM #locpar p
JOIN $(A2Base).dbo.Location a ON a.LookupId COLLATE DATABASE_DEFAULT = p.LookupId
JOIN $(A3Base).dbo.Location b ON b.LookupId COLLATE DATABASE_DEFAULT = p.LookupId
WHERE p.A3Gyldig = 1 AND p.WkbUlik = 1
  AND b.Geometry.STGeometryType() = 'Point' AND a.Geometry.STGeometryType() <> 'Point';

DECLARE @lokfelles bigint = (SELECT COUNT_BIG(*) FROM #locpar);

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'Location', 'Bare i Artskart 2',
       (SELECT COUNT_BIG(*) FROM (
            SELECT LookupId COLLATE DATABASE_DEFAULT k FROM $(A2Base).dbo.Location WHERE NodeId IN (SELECT NodeId FROM @noder)
            EXCEPT SELECT LookupId COLLATE DATABASE_DEFAULT FROM $(A3Base).dbo.Location) x),
       NULL, 1, 'Lokalitetsnokkelen inneholder ost, nord og presisjon, sa et ProjNet-skift eller en annotert presisjon flytter nokkelen.'
UNION ALL
SELECT @kjoring, 'Location', 'Bare i Artskart 3',
       (SELECT COUNT_BIG(*) FROM (
            SELECT LookupId COLLATE DATABASE_DEFAULT k FROM $(A3Base).dbo.Location
            EXCEPT SELECT LookupId COLLATE DATABASE_DEFAULT FROM $(A2Base).dbo.Location WHERE NodeId IN (SELECT NodeId FROM @noder)) x),
       NULL, 1, 'Som over.'
UNION ALL
SELECT @kjoring, 'Location', 'Feltavvik (ost, nord, lat, lon, presisjon, Locality, LocationId)',
       (SELECT COUNT_BIG(*) FROM #locpar WHERE FeltUlik = 1), @lokfelles, 0, NULL
UNION ALL
SELECT @kjoring, 'Location', 'Geometriavvik (WKB)',
       (SELECT COUNT_BIG(*) FROM #locpar WHERE WkbUlik = 1), @lokfelles, 0, NULL
UNION ALL
SELECT @kjoring, 'Location', 'Ugyldig geometri i Artskart 3',
       (SELECT COUNT_BIG(*) FROM #locpar WHERE A3Gyldig = 0), @lokfelles, 0,
       'Artskart 2 validerer med SQL Servers STIsValid og reparerer; Artskart 3 avviser pa NTS sin IsValid. Se GeographyService.TryTolk.'
UNION ALL
SELECT @kjoring, 'Location', 'Ugyldig i Artskart 3, lik Artskart 2 etter MakeValid',
       (SELECT COUNT_BIG(*) FROM #locpar WHERE A3GyldigEtterMakeValidLikA2 = 1), @lokfelles, 1,
       'Artskart 2 lagrer den reparerte geometrien. Artskart 3 lagrer den ureparerte.'
UNION ALL
SELECT @kjoring, 'Location', 'Artskart 3 lagret punkt der Artskart 2 lagret omriss',
       (SELECT COUNT_BIG(*) FROM #locpar WHERE A3ErPunktA2ErOmriss = 1), @lokfelles, 0,
       'Omrisset er tapt. TryTolk avviser pa NTS sin IsValid der Artskart 2 reparerer og beholder polygonet.';

----------------------------------------------------------------------------------------
-- 4. Koblingstabeller, på forretningsnøkkel
----------------------------------------------------------------------------------------

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationTags', 'Bare i Artskart 2', COUNT_BIG(*), NULL, 1,
       'Tag 7 (Blocked) og Tag 9 (AnnotatedChanges) settes bare av annotasjonstjenesten. Fase 10.'
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TagId FROM $(A2Base).dbo.ObservationTags t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TagId FROM $(A3Base).dbo.ObservationTags t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId) x;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationTags', 'Bare i Artskart 3', COUNT_BIG(*), NULL, 0, NULL
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TagId FROM $(A3Base).dbo.ObservationTags t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TagId FROM $(A2Base).dbo.ObservationTags t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)) x;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationTaxons', 'Bare i Artskart 2', COUNT_BIG(*), NULL, 0, NULL
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TaxonId FROM $(A2Base).dbo.ObservationTaxons t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TaxonId FROM $(A3Base).dbo.ObservationTaxons t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId) x;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationTaxons', 'Bare i Artskart 3', COUNT_BIG(*), NULL, 0, NULL
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TaxonId FROM $(A3Base).dbo.ObservationTaxons t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TaxonId FROM $(A2Base).dbo.ObservationTaxons t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)) x;

-- ObservationTaxons er taksonhierarkiet for posten, så en annotert art eller et nytt
-- takson i referansedataene slår ut i flere rader per post. Målingen som avgjør er om
-- avviket finnes på en post som verken er annotert, blokkert eller rammet av
-- taksonomisnapshotet — det er i så fall porten som avviker.
INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationTaxons', 'Avvik pa en post uten annotasjon eller taksonskifte',
       COUNT_BIG(*), NULL, 0,
       'Null her betyr at hele avviket folger av fase 10 og ulikt taksonomisnapshot.'
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TaxonId FROM $(A2Base).dbo.ObservationTaxons t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TaxonId FROM $(A3Base).dbo.ObservationTaxons t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId
    UNION
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.TaxonId FROM $(A3Base).dbo.ObservationTaxons t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.TaxonId FROM $(A2Base).dbo.ObservationTaxons t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)) x
WHERE x.k NOT IN (
    SELECT ProxyId FROM #par
    WHERE Annotert = 1 OR Blokkert = 1 OR ISNULL(a_Takson,-1) <> ISNULL(b_Takson,-1));

IF (SELECT Antall FROM dbo.Avvik
    WHERE KjoringId = @kjoring AND Omrade = 'ObservationTaxons'
      AND Maal = 'Avvik pa en post uten annotasjon eller taksonskifte') = 0
BEGIN
    UPDATE dbo.Avvik
    SET Forklart = 1,
        Forklaring = 'Ligger bare pa annoterte/blokkerte poster og poster rammet av taksonomisnapshotet.'
    WHERE KjoringId = @kjoring AND Omrade = 'ObservationTaxons'
      AND Maal IN ('Bare i Artskart 2', 'Bare i Artskart 3');
END;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationBehaviors', 'Bare i Artskart 2', COUNT_BIG(*), NULL, 0, NULL
FROM (
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT k, t.BehaviorId FROM $(A2Base).dbo.ObservationBehaviors t
    JOIN $(A2Base).dbo.Observation o ON o.Id = t.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder)
    EXCEPT
    SELECT o.ProxyId COLLATE DATABASE_DEFAULT, t.BehaviorId FROM $(A3Base).dbo.ObservationBehaviors t
    JOIN $(A3Base).dbo.Observation o ON o.Id = t.ObservationId) x;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'ObservationError', 'Radtall Artskart 2 mot Artskart 3',
       ABS((SELECT COUNT_BIG(*) FROM $(A2Base).dbo.ObservationError e
            JOIN $(A2Base).dbo.Observation o ON o.Id = e.ObservationId WHERE o.NodeId IN (SELECT NodeId FROM @noder))
         - (SELECT COUNT_BIG(*) FROM $(A3Base).dbo.ObservationError)),
       (SELECT COUNT_BIG(*) FROM $(A3Base).dbo.ObservationError), 0, NULL;

----------------------------------------------------------------------------------------
-- 5. Områdekoblinger
--
-- OMRÅDE-ID-ER KAN IKKE SAMMENLIGNES. De to basene har ulike områdesnapshot, så
-- nøkkelen er lokalitetens LookupId pluss Area.Fid.
--
-- OG ARTSKART 3 FILTRERER TIL GJELDENDE OMRÅDER med vilje. Artskart 2 kobler også til
-- historiske områder — Aust-Agder i tillegg til Agder, to rader for samme kommune — og
-- de skal ikke med. Avviket deles derfor i to: de historiske, som er vedtatt, og de
-- gjeldende, som skal matche.
----------------------------------------------------------------------------------------

CREATE TABLE #a2la (k nvarchar(100) COLLATE DATABASE_DEFAULT, f nvarchar(60) COLLATE DATABASE_DEFAULT);
CREATE TABLE #a3la (k nvarchar(100) COLLATE DATABASE_DEFAULT, f nvarchar(60) COLLATE DATABASE_DEFAULT);

INSERT #a2la
SELECT l.LookupId COLLATE DATABASE_DEFAULT, a.Fid COLLATE DATABASE_DEFAULT
FROM $(A2Base).dbo.LocationAreas la
JOIN $(A2Base).dbo.Location l ON l.Id = la.LocationId
JOIN $(A2Base).dbo.Area a ON a.Id = la.AreaId
WHERE l.NodeId IN (SELECT NodeId FROM @noder) AND a.IsCurrent = 1;

INSERT #a3la
SELECT l.LookupId COLLATE DATABASE_DEFAULT, a.Fid COLLATE DATABASE_DEFAULT
FROM $(A3Base).dbo.LocationAreas la
JOIN $(A3Base).dbo.Location l ON l.Id = la.LocationId
JOIN $(A3Base).dbo.Area a ON a.Id = la.AreaId;

INSERT dbo.Avvik (KjoringId, Omrade, Maal, Antall, Av, Forklart, Forklaring)
SELECT @kjoring, 'LocationAreas', 'Historiske koblinger bare i Artskart 2',
       (SELECT COUNT_BIG(*) FROM $(A2Base).dbo.LocationAreas la
        JOIN $(A2Base).dbo.Location l ON l.Id = la.LocationId
        JOIN $(A2Base).dbo.Area a ON a.Id = la.AreaId
        WHERE l.NodeId IN (SELECT NodeId FROM @noder) AND a.IsCurrent = 0),
       NULL, 1, 'Artskart 3 filtrerer til gjeldende omrader i GeographyService. Vedtatt avvik.'
UNION ALL
SELECT @kjoring, 'LocationAreas', 'Gjeldende koblinger bare i Artskart 2',
       (SELECT COUNT_BIG(*) FROM (SELECT * FROM #a2la EXCEPT SELECT * FROM #a3la) x),
       (SELECT COUNT_BIG(*) FROM #a2la), 0, NULL
UNION ALL
SELECT @kjoring, 'LocationAreas', 'Gjeldende koblinger bare i Artskart 3',
       (SELECT COUNT_BIG(*) FROM (SELECT * FROM #a3la EXCEPT SELECT * FROM #a2la) x),
       (SELECT COUNT_BIG(*) FROM #a3la), 0, NULL
UNION ALL
SELECT @kjoring, 'LocationAreas', 'Gjeldende avvik pa en lokalitet som finnes i BEGGE basene',
       (SELECT COUNT_BIG(*) FROM (SELECT * FROM #a2la EXCEPT SELECT * FROM #a3la) x
        WHERE x.k IN (SELECT LookupId COLLATE DATABASE_DEFAULT FROM #locpar))
     + (SELECT COUNT_BIG(*) FROM (SELECT * FROM #a3la EXCEPT SELECT * FROM #a2la) x
        WHERE x.k IN (SELECT LookupId COLLATE DATABASE_DEFAULT FROM #locpar)),
       NULL, 0,
       'Null her betyr at alle omradeavvik ligger pa lokaliteter som bare finnes i en base.';

-- Er avviket null på lokalitetene som finnes i begge basene, så er områdekoblingene
-- identiske der de kan sammenlignes, og de to settene «bare i …» følger bare av at
-- lokalitetsnøkkelen flyttet seg. Da er de forklart av ProjNet, ikke av områdeoppslaget.
IF (SELECT Antall FROM dbo.Avvik
    WHERE KjoringId = @kjoring AND Omrade = 'LocationAreas'
      AND Maal = 'Gjeldende avvik pa en lokalitet som finnes i BEGGE basene') = 0
BEGIN
    UPDATE dbo.Avvik
    SET Forklart = 1,
        Forklaring = 'Ligger bare pa lokaliteter som finnes i en base. Folger av at nokkelen flyttet seg (ProjNet).'
    WHERE KjoringId = @kjoring AND Omrade = 'LocationAreas'
      AND Maal IN ('Gjeldende koblinger bare i Artskart 2', 'Gjeldende koblinger bare i Artskart 3');
END;

----------------------------------------------------------------------------------------
-- Rapport
----------------------------------------------------------------------------------------

-- RESTEN ER TALLET SOM BETYR NOE. En kolonne med 4 013 avvik der 3 993 er attribuert
-- har 20 å undersøke, og «4 013 UFORKLART» leser som noe helt annet. Resten vises
-- derfor ved siden av, og den sorterer rapporten.
SELECT a.Omrade, a.Maal, a.Antall, a.Av,
       Rest = CASE WHEN f.Antall IS NULL THEN NULL ELSE a.Antall - f.Antall END,
       Status = CASE WHEN a.Antall = 0 THEN 'likt'
                     WHEN a.Forklart = 1 THEN 'forklart'
                     WHEN f.Antall > 0 THEN '** REST ' + CAST(a.Antall - f.Antall AS varchar(12)) + ' **'
                     ELSE '** UFORKLART **' END,
       a.Forklaring
FROM dbo.Avvik a
LEFT JOIN dbo.Avvik f
  ON f.KjoringId = a.KjoringId AND f.Omrade = 'Observation forklart' AND f.Maal = a.Maal
     AND a.Omrade = 'Observation'
WHERE a.KjoringId = @kjoring
ORDER BY CASE WHEN a.Antall = 0 THEN 3 WHEN a.Forklart = 1 THEN 2
              WHEN f.Antall > 0 THEN 1 ELSE 0 END,
         a.Omrade, a.Maal;

DROP TABLE #a2obs; DROP TABLE #a3obs; DROP TABLE #par;
DROP TABLE #locpar; DROP TABLE #a2la; DROP TABLE #a3la;

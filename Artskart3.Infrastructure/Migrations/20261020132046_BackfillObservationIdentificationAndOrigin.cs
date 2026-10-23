using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller artsbestemmelse og opprinnelse på begge tabellene, og tar
/// indekskolonnene inn i columnstore.
///
/// REKKEFØLGEN ER IKKE VALGFRI
/// Observation fylles fra ObservationTags, og ObservationEntityIndex fylles fra
/// Observation. Indekstabellen arver dermed verifiseringen av Observation i
/// stedet for å gjenta koblingen mot taggene. Samme kjede som
/// BackfillObservationAnnotationAndSensitive og dens oppfølger.
///
///   UncertainIdentification  ←  TagId 2  (UnsureIdentification)   102 388 funn
///   Introduced               ←  TagId 1  (Unspontaneus)            43 347 funn
///
/// STATISTIKKOPPDATERINGEN ER PÅKREVD
/// 146k endringer på 61 millioner rader ligger under terskelen for automatisk
/// oppdatering. Uten den beholder de filtrerte indeksene statistikken de hadde
/// da de var tomme, og optimizeren velger plan på feil grunnlag.
///
/// COLUMNSTORE SLIPPES OG BYGGES PÅ NYTT
/// En columnstore-indeks kan ikke utvides med ALTER. Den slippes først, og
/// bygges til slutt med de to nye kolonnene i lista. Mens migrasjonen går
/// finnes den ikke, og områdetellingene er ~10x tregere i det vinduet.
/// Bygget tok 13 min sist.
///
/// Kolonnelista her skal IKKE speiles i Scripts/BackfillAll.sql: det skriptet
/// kjører fra migrasjon 20260828065007, før disse kolonnene finnes. Det går bra
/// fordi denne migrasjonen alltid kjører etterpå og bygger indeksen med full
/// liste. Samme forhold som for LocationId og MonthCollected.
///
/// suppressTransaction: indeksbygget tar langt over timeout-grensen.
/// </summary>
public partial class BackfillObservationIdentificationAndOrigin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- Slipp columnstore først -----------------------------------------
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Slipper IX_OEI_Columnstore (bygges paa nytt til slutt)...', 0, 1) WITH NOWAIT;
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;
END
", suppressTransaction: true);

        // --- Observation fra taggene ------------------------------------------
        //
        // Oppfyllbart predikat: raden matcher ikke etter oppdateringen, så en ny
        // kjøring etter avbrudd gjør bare det som gjenstår. Derfor ikke vannmerke.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Rows INT, @Msg NVARCHAR(200);

UPDATE o SET o.UncertainIdentification = 1
FROM dbo.Observation o
INNER JOIN dbo.ObservationTags ot ON ot.ObservationId = o.Id
WHERE ot.TagId = 2 AND o.UncertainIdentification = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Observation.UncertainIdentification: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

UPDATE o SET o.Introduced = 1
FROM dbo.Observation o
INNER JOIN dbo.ObservationTags ot ON ot.ObservationId = o.Id
WHERE ot.TagId = 1 AND o.Introduced = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Observation.Introduced: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

UPDATE STATISTICS dbo.Observation (IX_Observation_UncertainIdentification) WITH FULLSCAN;
UPDATE STATISTICS dbo.Observation (IX_Observation_Introduced) WITH FULLSCAN;
", suppressTransaction: true);

        // --- Indekstabellen fra Observation ------------------------------------
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Rows INT, @Msg NVARCHAR(200);

UPDATE idx SET idx.UncertainIdentification = 1
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.UncertainIdentification = 1 AND idx.UncertainIdentification = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Indeks.UncertainIdentification: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

UPDATE idx SET idx.Introduced = 1
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.Introduced = 1 AND idx.Introduced = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Indeks.Introduced: ', FORMAT(@Rows,'N0'), ' rader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
", suppressTransaction: true);

        // --- Bygg columnstore med de nye kolonnene ----------------------------
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore med artsbestemmelse og opprinnelse (10-30 min)...', 0, 1) WITH NOWAIT;

    CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
    ON dbo.ObservationEntityIndex
        (ObservationId, EntityTypeId, EntityId,
         TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
         HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
         SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
         InstitutionOrgId, DatasetOrgId, BehaviorId,
         LocationId, MonthCollected,
         ChangedByAnnotation, Sensitive,
         UncertainIdentification, Introduced)
    WITH (MAXDOP = 4);

    RAISERROR('IX_OEI_Columnstore opprettet.', 0, 1) WITH NOWAIT;
END
", suppressTransaction: true);

        // --- Verifisering ------------------------------------------------------
        //
        // To uavhengige kontroller: Observation mot taggene, og indeksen mot
        // Observation. Slår én av dem til, heves feilen og migrasjonen regnes
        // ikke som fullført.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @AvvikKilde INT, @AvvikIndeks INT, @Msg NVARCHAR(900);

SELECT @AvvikKilde = COUNT(*)
FROM dbo.Observation o
WHERE o.UncertainIdentification <>
        CASE WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                          WHERE ot.ObservationId = o.Id AND ot.TagId = 2) THEN 1 ELSE 0 END
   OR o.Introduced <>
        CASE WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags ot
                          WHERE ot.ObservationId = o.Id AND ot.TagId = 1) THEN 1 ELSE 0 END;

SELECT @AvvikIndeks = COUNT(*)
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE idx.UncertainIdentification <> o.UncertainIdentification
   OR idx.Introduced <> o.Introduced;

IF @AvvikKilde > 0 OR @AvvikIndeks > 0
BEGIN
    SET @Msg = CONCAT(
        'Verifisering FEILET: ', FORMAT(@AvvikKilde,'N0'), ' rader paa Observation stemmer ikke ',
        'med taggene, og ', FORMAT(@AvvikIndeks,'N0'), ' indeksrader stemmer ikke med Observation. ',
        'MERK at en ny kjoering bare retter EN retning: UPDATE-ene setter 0 til 1, aldri 1 til 0. ',
        'Rader som mangler flagget der taggen finnes rettes av en ny kjoering. Rader som HAR ',
        'flagget satt uten at taggen finnes gjoer det ikke - de maa nullstilles manuelt, ellers ',
        'feiler denne verifiseringen likt ved hver eneste deploy og migrasjonen blir aldri ',
        'registrert. Avviksretningen maa altsaa undersoekes foer du kjoerer paa nytt.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - artsbestemmelse og opprinnelse stemmer hele kjeden.', 0, 1) WITH NOWAIT;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Columnstore bygges tilbake uten de to kolonnene. Kolonnene selv fjernes
        // av forrige migrasjons Down.
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
    DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;

CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
ON dbo.ObservationEntityIndex
    (ObservationId, EntityTypeId, EntityId,
     TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
     HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
     SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
     InstitutionOrgId, DatasetOrgId, BehaviorId,
     LocationId, MonthCollected,
     ChangedByAnnotation, Sensitive)
WITH (MAXDOP = 4);
", suppressTransaction: true);
    }
}

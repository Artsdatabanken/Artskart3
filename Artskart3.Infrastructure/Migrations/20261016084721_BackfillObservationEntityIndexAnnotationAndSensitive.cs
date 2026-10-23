using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Fyller ObservationEntityIndex.ChangedByAnnotation og Sensitive, og tar begge
/// kolonnene inn i columnstore.
///
/// De hører sammen: kolonnen uten columnstore gir ingen gevinst, siden hele
/// poenget er at aggregeringen skal kunne gå i batch mode. Samme mønster som
/// BackfillObservationEntityIndexLocationIdAndMonth.
///
/// INGEN BATCHING HER
/// Til forskjell fra LocationId-backfillen, som måtte gjennom alle 134,7
/// millioner radene, treffer denne bare indeksradene som hører til en merket
/// observasjon: 133 357 annoterte og 2 194 sensitive, altså noen hundre tusen
/// indeksrader. Det er én kort UPDATE per kolonne, ikke en løkke med vannmerke.
///
/// Kilden er Observation, ikke ObservationTags. Kolonnene der ble fylt av
/// BackfillObservationAnnotationAndSensitive og er allerede verifisert mot
/// taggene, så vi arver den verifiseringen i stedet for å gjenta koblingen.
///
/// COLUMNSTORE SLIPPES OG BYGGES PÅ NYTT
/// En columnstore-indeks kan ikke utvides med ALTER. Den slippes derfor først.
/// Her er ikke begrunnelsen deltastore-skriving — oppdateringen er liten — men
/// at indeksen uansett må bygges om for å få med de nye kolonnene.
///
/// Konsekvens: mens migrasjonen går, finnes ikke columnstore, og
/// områdetellingene er ~10x tregere i det vinduet. Bygget tar 10-30 min.
///
/// KOLONNELISTEN OG Scripts/BackfillAll.sql
/// BackfillAll eier indeksen ved en full datafylling, men kjører fra migrasjon
/// 20260828065007 — før disse kolonnene finnes. De skal derfor IKKE legges til
/// i kolonnelista der; da ville et ferskt miljø feile på en kolonne som ennå
/// ikke er opprettet. Det samme gjelder allerede LocationId og MonthCollected.
///
/// Det går likevel bra, fordi denne migrasjonen alltid kjører etterpå og
/// slipper indeksen før den bygger den opp igjen med full liste.
///
/// suppressTransaction: indeksbygget tar langt over timeout-grensen. I én
/// transaksjon ville et avbrudd rullet tilbake alt.
/// </summary>
public partial class BackfillObservationEntityIndexAnnotationAndSensitive : Migration
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

        // --- Backfill ---------------------------------------------------------
        //
        // Oppfyllbart predikat per kolonne: raden matcher ikke etter
        // oppdateringen, så en ny kjøring etter et avbrudd gjør bare det som
        // gjenstår. Derfor trengs ikke vannmerke.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Rows INT, @Msg NVARCHAR(200);

UPDATE idx SET idx.ChangedByAnnotation = 1
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.ChangedByAnnotation = 1 AND idx.ChangedByAnnotation = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('ChangedByAnnotation: ', FORMAT(@Rows,'N0'), ' indeksrader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;

UPDATE idx SET idx.Sensitive = 1
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE o.Sensitive = 1 AND idx.Sensitive = 0;
SET @Rows = @@ROWCOUNT;
SET @Msg = CONCAT('Sensitive: ', FORMAT(@Rows,'N0'), ' indeksrader.');
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
", suppressTransaction: true);

        // --- Bygg columnstore med de nye kolonnene ----------------------------
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_OEI_Columnstore'
                 AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Oppretter IX_OEI_Columnstore med ChangedByAnnotation og Sensitive (10-30 min)...', 0, 1) WITH NOWAIT;

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

    RAISERROR('IX_OEI_Columnstore opprettet.', 0, 1) WITH NOWAIT;
END
", suppressTransaction: true);

        // --- Verifisering ------------------------------------------------------
        //
        // Leser mot Observation, ikke mot et vannmerke: det eneste som avgjør er
        // om indeksen sier det samme som kilden. Slår den til, heves feilen, og
        // migrasjonen regnes ikke som fullført.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Avvik INT;

SELECT @Avvik = COUNT(*)
FROM dbo.ObservationEntityIndex idx
INNER JOIN dbo.Observation o ON o.Id = idx.ObservationId
WHERE idx.ChangedByAnnotation <> o.ChangedByAnnotation
   OR idx.Sensitive <> o.Sensitive;

IF @Avvik > 0
BEGIN
    DECLARE @Msg NVARCHAR(800) = CONCAT(
        'Verifisering FEILET: ', FORMAT(@Avvik,'N0'),
        ' indeksrader stemmer ikke med Observation. MERK at en ny kjoering bare retter EN ',
        'retning: UPDATE-ene setter 0 til 1, aldri 1 til 0. Indeksrader som mangler flagget ',
        'rettes av en ny kjoering. Indeksrader som HAR flagget satt der Observation har 0 ',
        'gjoer det ikke - de maa nullstilles manuelt, ellers feiler denne verifiseringen likt ',
        'ved hver deploy. Observation settes aldri fra 1 til 0 av migrasjonene, saa et slikt ',
        'avvik betyr at noe utenfor migrasjonene har skrevet til kolonnen.');
    RAISERROR(@Msg, 16, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('Verifisering OK - annotering og sensitivitet stemmer med Observation.', 0, 1) WITH NOWAIT;
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
     LocationId, MonthCollected)
WITH (MAXDOP = 4);
", suppressTransaction: true);
    }
}

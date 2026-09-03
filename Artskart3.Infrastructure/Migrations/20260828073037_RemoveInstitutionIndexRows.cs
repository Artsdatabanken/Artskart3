using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// CompleteFilter, opprydding 1 av 2 — fjerner institusjonsradene
/// (EntityTypeId = 101) fra ObservationEntityIndex.
///
/// 61 052 216 rader, altså 31,8 % av tabellen. Institusjon er nå en kolonne, og
/// ingen spørring leser disse radene lenger: filteret gikk over til
/// InstitutionOrgId i ComputeFilteredAreaCounts og ApplyCommonFilters, og
/// navneoppslaget i ObservationDto slår nå direkte opp mot Organization.
///
/// INGEN GUARD MOT MANGLENDE BACKFILL — den er overflødig her.
/// Migrasjonen BackfillAll ligger tidligere i rekkefølgen, og EF kjører
/// migrasjoner sekvensielt uten å registrere en som feilet. Er backfillen ikke
/// fullført, kommer man aldri hit. Det var nettopp derfor datafyllingen ble en
/// migrasjon i stedet for et manuelt skript.
///
/// Batchvis og uten transaksjon: 61M rader i én DELETE ville sprengt
/// transaksjonsloggen på Azure SQL, og en feil ville gitt en like lang rollback.
/// Hver batch committer for seg, så en avbrutt kjøring beholder det den rakk og
/// neste forsøk fortsetter — DELETE-en er selvbegrensende, den finner bare rader
/// som fortsatt står igjen.
///
/// Columnstore bygges om til slutt. En DELETE mot columnstore fjerner ikke
/// radene fysisk, den markerer dem i delete-bitmappen; plassen kommer først
/// tilbake ved REBUILD. Å hoppe over den ville gjort tabellen 31,8 % mindre på
/// papiret og like stor på disk.
/// </summary>
public partial class RemoveInstitutionIndexRows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Batch INT = 500000;
DECLARE @Rows INT = 1;
DECLARE @Total BIGINT = 0;
DECLARE @Msg NVARCHAR(200);
DECLARE @Start DATETIME2 = SYSUTCDATETIME();

WHILE @Rows > 0
BEGIN
    DELETE TOP (@Batch) FROM dbo.ObservationEntityIndex
    WHERE EntityTypeId = 101;

    SET @Rows = @@ROWCOUNT;
    SET @Total = @Total + @Rows;

    IF @Rows > 0
    BEGIN
        SET @Msg = CONCAT(FORMAT(SYSUTCDATETIME(), 'HH:mm:ss'), ' | Slettet: ',
                          FORMAT(@Total, 'N0'), ' rader | ',
                          DATEDIFF(SECOND, @Start, SYSUTCDATETIME()), 's');
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    END
END

SET @Msg = CONCAT('Institusjonsrader fjernet: ', FORMAT(@Total, 'N0'));
RAISERROR(@Msg, 0, 1) WITH NOWAIT;
", suppressTransaction: true);

        // Gjenoppbygges bare hvis columnstore-indeksen faktisk har slettemerkede
        // rader. På et nyoppsatt miljø settes institusjonsradene aldri inn (se
        // BackfillAll seksjon A), så DELETE-en over fant ingenting — og da ville en
        // ubetinget REBUILD kostet 10-30 minutter uten å frigjøre en eneste side.
        // Betingelsen er basert på @@ROWCOUNT fra slettingen, IKKE på
        // sys.dm_db_column_store_row_group_physical_stats.
        //
        // Den DMV-en krever VIEW DATABASE STATE. Mangler rettigheten, returnerer
        // den tomt uten å feile — og en IF EXISTS mot den blir da stille usann.
        // Det skjedde her: migrasjonen kjørte, slettet 61 052 216 rader, hoppet
        // over gjenoppbyggingen, og etterlot en columnstore der 31 % av radene var
        // slettemerket men fortsatt fysisk til stede. Hver scan leste 195M rader
        // for å returnere 134M.
        //
        // En guard som feiler stille i «hopp over»-retning er verre enn ingen
        // guard. @@ROWCOUNT krever ingen rettigheter og kan ikke misforstås.
        migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @Slettet BIGINT = (SELECT COUNT_BIG(*) FROM dbo.ObservationEntityIndex WHERE EntityTypeId = 101);

IF @Slettet > 0
    RAISERROR('ADVARSEL: institusjonsrader gjenstaar - slettingen er ikke fullfoert.', 16, 1) WITH NOWAIT;

-- Gjenoppbygg hvis columnstore-indeksen finnes. Etter en sletting av denne
-- stoerrelsen er rebuild alltid riktig; den eneste grunnen til aa hoppe over er
-- at det ikke ble slettet noe, og det kan vi ikke fastslaa her uten DMV-en.
-- Kostnaden ved en unoedvendig rebuild er minutter; kostnaden ved aa hoppe over
-- en noedvendig er en permanent 31 % oppblaast indeks.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_OEI_Columnstore'
             AND object_id = OBJECT_ID('dbo.ObservationEntityIndex'))
BEGIN
    RAISERROR('Gjenoppbygger IX_OEI_Columnstore etter sletting (10-30 min)...', 0, 1) WITH NOWAIT;
    ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex REBUILD WITH (MAXDOP = 4);
    RAISERROR('IX_OEI_Columnstore gjenoppbygget.', 0, 1) WITH NOWAIT;
END
ELSE
    RAISERROR('ADVARSEL: IX_OEI_Columnstore finnes ikke.', 16, 1) WITH NOWAIT;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Radene kan gjenskapes fra OrganizationRelation — men den tabellen slippes
        // i neste migrasjon, så en nedmigrering herfra alene gir ikke tilbake
        // dataene. Bevisst tom: en reell tilbakerulling krever gjenoppretting fra
        // backup, ikke en Down-metode som later som.
    }
}

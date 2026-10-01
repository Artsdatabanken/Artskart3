using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tabellene for lokasjonsbufferen. Oppretter dem tomme — de fylles av
/// byggejobben.
///
/// INERT TIL BYGGEJOBBEN FINNES
/// LocationCountCacheState får ingen rad her. Oppslaget krever Status = 'Ready',
/// så uten byggejobben faller hvert eneste kall gjennom til aggregeringen i
/// GetLocationsAsync og oppførselen er uendret. Migrasjonen kan altså deployes
/// før jobben.
///
/// NØKKELREKKEFØLGE
/// (DimensionId, BucketId, East, North, LocationId). Dimensjon og bøtte først,
/// deretter kartutsnittet — det er nøyaktig oppslagsretningen, og utsnittet er
/// det som snevrer mest inn: 185 x 103 km av et land på 2500 km.
///
/// LocationId står sist. Den er bare med for å gjøre nøkkelen unik og gi en
/// deterministisk tiebreaker ved like antall; det søkes aldri på den.
///
/// STØRRELSE
/// Anslått ~77 millioner rader for de 13 dimensjonene pluss den ufiltrerte,
/// rundt 2 GB med sidekomprimering. Anslaget er kalibrert mot
/// AreaCountCacheLevel2, som måler 26,4 byte per rad komprimert.
/// </summary>
public partial class AddLocationCountCacheTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LocationCountCacheLevel1",
            columns: table => new
            {
                DimensionId = table.Column<byte>(type: "tinyint", nullable: false),
                BucketId = table.Column<int>(type: "int", nullable: false),
                LocationId = table.Column<int>(type: "int", nullable: false),
                East = table.Column<int>(type: "int", nullable: false),
                North = table.Column<int>(type: "int", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocationCountCacheLevel1", x => new { x.DimensionId, x.BucketId, x.East, x.North, x.LocationId });
            });

        migrationBuilder.CreateTable(
            name: "LocationCountCacheState",
            columns: table => new
            {
                Id = table.Column<byte>(type: "tinyint", nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                BuiltAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SourceRows = table.Column<long>(type: "bigint", nullable: true),
                DurationSeconds = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocationCountCacheState", x => x.Id);
            });

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION, så
        // den settes med rå SQL. Samme mønster som AreaCountCacheLevel1. Bare den
        // store tabellen: tilstandstabellen har én rad.
        migrationBuilder.Sql(
            "ALTER TABLE dbo.LocationCountCacheLevel1 REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LocationCountCacheLevel1");
        migrationBuilder.DropTable(name: "LocationCountCacheState");
    }
}

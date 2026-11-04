using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tabellene for områdebufferen. Oppretter dem tomme — de fylles av byggejobben.
///
/// INERT TIL BYGGEJOBBEN FINNES
/// AreaCountCacheState får ingen rad her. Oppslaget krever Status = 'Ready', så uten
/// byggejobben faller hvert eneste kall gjennom til ComputeFilteredAreaCounts og
/// oppførselen er uendret. Det er med vilje: migrasjonen kan deployes før jobben.
///
/// NØKKELREKKEFØLGE
/// Klyngenøklene speiler oppslagsretningen: dimensjon og bøtte først, utdatacellen
/// sist. Et oppslag blir ett sammenhengende rekkeviddesøk, og intervalldimensjonene
/// (periode, koordpresisjon) får bøtteintervallet sitt langs nøkkelen framfor som
/// restledd over hele dimensjonen.
///
/// AreaCountCacheBucketMember har MemberId før BucketId fordi oppslaget alltid går
/// «gitt verneområde 575, hvilke bøtter inneholder det» — aldri motsatt vei.
///
/// STØRRELSE
/// Målt på produksjonslik data: ettnivå 1 448 814 rader, toernivå 36 320 085.
/// Ukomprimert er toernivået ~0,9 GB; med sidekomprimering omtrent halvparten.
/// Byggejobben bygger blue/green, så disken må ha plass til to sett samtidig —
/// budsjetter ~2 GB.
///
/// EntityTypeId er int, ikke tinyint. Områdetype-id-ene er ikke garantert små
/// (integrasjonstestene bruker 910001), og en tinyint ville avkortet dem stille.
/// </summary>
public partial class AddAreaCountCacheTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AreaCountCacheBucketMember",
            columns: table => new
            {
                DimensionId = table.Column<byte>(type: "tinyint", nullable: false),
                MemberId = table.Column<int>(type: "int", nullable: false),
                BucketId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AreaCountCacheBucketMember", x => new { x.DimensionId, x.MemberId, x.BucketId });
            });

        migrationBuilder.CreateTable(
            name: "AreaCountCacheLevel1",
            columns: table => new
            {
                DimensionId = table.Column<byte>(type: "tinyint", nullable: false),
                BucketId = table.Column<int>(type: "int", nullable: false),
                EntityTypeId = table.Column<int>(type: "int", nullable: false),
                EntityId = table.Column<int>(type: "int", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AreaCountCacheLevel1", x => new { x.DimensionId, x.BucketId, x.EntityTypeId, x.EntityId });
            });

        migrationBuilder.CreateTable(
            name: "AreaCountCacheLevel2",
            columns: table => new
            {
                DimensionPairId = table.Column<byte>(type: "tinyint", nullable: false),
                BucketA = table.Column<int>(type: "int", nullable: false),
                BucketB = table.Column<int>(type: "int", nullable: false),
                EntityTypeId = table.Column<int>(type: "int", nullable: false),
                EntityId = table.Column<int>(type: "int", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AreaCountCacheLevel2", x => new { x.DimensionPairId, x.BucketA, x.BucketB, x.EntityTypeId, x.EntityId });
            });

        migrationBuilder.CreateTable(
            name: "AreaCountCacheState",
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
                table.PrimaryKey("PK_AreaCountCacheState", x => x.Id);
            });

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION, så den
        // settes med rå SQL. Samme mønster som IX_Observation_CatalogNumber.
        // Bare de to store tabellene: medlems- og tilstandstabellen er små nok til at
        // komprimering bare koster CPU.
        migrationBuilder.Sql(@"
ALTER TABLE dbo.AreaCountCacheLevel1 REBUILD WITH (DATA_COMPRESSION = PAGE);
ALTER TABLE dbo.AreaCountCacheLevel2 REBUILD WITH (DATA_COMPRESSION = PAGE);
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AreaCountCacheBucketMember");
        migrationBuilder.DropTable(name: "AreaCountCacheLevel1");
        migrationBuilder.DropTable(name: "AreaCountCacheLevel2");
        migrationBuilder.DropTable(name: "AreaCountCacheState");
    }
}

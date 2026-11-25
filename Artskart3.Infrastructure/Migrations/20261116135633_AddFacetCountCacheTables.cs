using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tabellene for fasett-bufferen — antall observasjoner ved hvert valg i
/// filterpanelet. Oppretter dem tomme; de fylles av BuildFacetCountCache.sql.
///
/// INERT TIL BYGGEJOBBEN HAR KJØRT
/// FacetCountCacheState får ingen rad her. Oppslaget krever Status = 'Ready', så uten
/// bygget faller hvert kall gjennom til aggregering og oppførselen er uendret. Egen
/// tilstandstabell nettopp for dette: tok fasettoppslaget områdebufferens rad, som alt
/// står på 'Ready', ville det lest tomme tabeller som «null treff» og vist 0 ved hvert
/// filtervalg.
///
/// SAMME BØTTER SOM OMRÅDEBUFFEREN, UTEN UTDATACELLEN
/// Dimensjons-id-er, pargruppe-id-er og filtermasken er delt med AreaCountCacheLevel1
/// og -Level2 — se FacetCountCacheDimensions. Det som er borte er EntityTypeId og
/// EntityId: her er svaret «antall per bøtte i én dimensjon», ikke «antall per område».
///
/// NØKKELREKKEFØLGE
/// Masken ligger sist, som i områdebufferen. Et oppslag uten bool-filter summerer over
/// maskene for samme bøtte, og de ligger da sammenhengende; med bool-filter er
/// predikatet (Mask &amp; Care) == Want, altså et restledd uansett hvor kolonnen står.
///
/// Toernivået har samme nøkkel uansett hvilken side fasetten ligger på. Med 4,39 mill.
/// rader fordelt på 66 par er et par i snitt ~66 000 rader, så oppslaget som må skanne
/// et helt par er billig — billigere enn en ekstra indeks for den andre retningen.
///
/// STØRRELSE
/// Målt på produksjonslik data: nullnivå 32 rader, ettnivå 68 834, toernivå 4 388 868.
/// Til sammenligning har områdebufferen 2 262 224 og 43 710 551.
/// </summary>
public partial class AddFacetCountCacheTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FacetCountCacheLevel0",
            columns: table => new
            {
                Mask = table.Column<byte>(type: "tinyint", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FacetCountCacheLevel0", x => x.Mask);
            });

        migrationBuilder.CreateTable(
            name: "FacetCountCacheLevel1",
            columns: table => new
            {
                DimensionId = table.Column<byte>(type: "tinyint", nullable: false),
                BucketId = table.Column<int>(type: "int", nullable: false),
                Mask = table.Column<byte>(type: "tinyint", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FacetCountCacheLevel1", x => new { x.DimensionId, x.BucketId, x.Mask });
            });

        migrationBuilder.CreateTable(
            name: "FacetCountCacheLevel2",
            columns: table => new
            {
                DimensionPairId = table.Column<byte>(type: "tinyint", nullable: false),
                BucketA = table.Column<int>(type: "int", nullable: false),
                BucketB = table.Column<int>(type: "int", nullable: false),
                Mask = table.Column<byte>(type: "tinyint", nullable: false),
                ObservationCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FacetCountCacheLevel2", x => new { x.DimensionPairId, x.BucketA, x.BucketB, x.Mask });
            });

        migrationBuilder.CreateTable(
            name: "FacetCountCacheState",
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
                table.PrimaryKey("PK_FacetCountCacheState", x => x.Id);
            });

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION, så den
        // settes med rå SQL. Samme mønster som AddAreaCountCacheTables og
        // AddLocationCountCacheTables.
        //
        // Bare toernivået. Null- og ettnivået er 32 og ~69 000 rader; der koster
        // komprimering bare CPU.
        migrationBuilder.Sql(
            "ALTER TABLE dbo.FacetCountCacheLevel2 REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FacetCountCacheLevel0");

        migrationBuilder.DropTable(name: "FacetCountCacheLevel1");

        migrationBuilder.DropTable(name: "FacetCountCacheLevel2");

        migrationBuilder.DropTable(name: "FacetCountCacheState");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tar FilterMask inn som tredje nøkkel på begge bufferne.
///
/// HVA SOM ENDRES
/// De fem bool-filtrene — bilder, annotering, sensitivitet, artsbestemmelse og
/// opprinnelse — var fem egne dimensjoner. Nå er de fem bit i én maske som ligger
/// SIST i primærnøkkelen på hver bufferrad.
///
/// HVORFOR
/// Bufferen svarer på maksimalt to dimensjoner. Som egne dimensjoner spiste to
/// bool-filtre hele budsjettet, og alt utover falt gjennom til databasen. Som
/// maske koster alle fem til sammen null dimensjonsplasser, så to vanlige filtre
/// pluss hvilken som helst bool-kombinasjon besvares fortsatt fra bufferen.
///
/// Dimensjonene går fra 17 til 12, pargruppene fra 136 til 66.
///
/// MÅLT KOSTNAD (se FilterMask for hele kjeden)
/// Masken multipliserer radene i en celle med antall masker som forekommer der —
/// 1,19x for fine par, 2,40x for grove, 1,06x per lokasjon. Områdebufferens nivå 2
/// anslås å vokse fra 41,7 til ~45 millioner rader; lokasjonsbufferen KRYMPER fra
/// 95,5 til ~74 millioner fordi fem dimensjoner forsvinner.
///
/// TABELLENE TØMMES FØRST
/// Å legge til en nøkkelkolonne på 41,7 og 95,5 millioner rader ville tatt lang
/// tid — og vært bortkastet, siden byggeskriptene tømmer og fyller tabellene rett
/// etterpå uansett. Her tømmes de først, slik at nøkkelendringen skjer på tomme
/// tabeller og er umiddelbar.
///
/// Status settes til 'Building' FØR tømmingen, etter samme mønster som
/// byggeskriptene: oppslaget leser bare 'Ready', så et kall mellom tømmingen og
/// neste bygg faller tilbake til telling i stedet for å lese en tom buffer og
/// svare med blankt kart.
///
/// ETTER DENNE MIGRASJONEN MÅ BEGGE BUFFERNE BYGGES.
/// Kjør Scripts/BuildAreaCountCache.sql og Scripts/BuildLocationCountCache.sql.
/// Inntil da teller alle kall som før — tregere, aldri galt.
///
/// SIDEKOMPRIMERING SETTES PÅ NYTT
/// Klyngenøkkelen bærer komprimeringen, og den går tapt når nøkkelen slippes og
/// opprettes. EF Core har ingen parameter for DATA_COMPRESSION.
/// </summary>
public partial class AddFilterMaskToCountCaches : Migration
{
    private static readonly string[] Tabeller =
    [
        "AreaCountCacheLevel1",
        "AreaCountCacheLevel2",
        "LocationCountCacheLevel1",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE dbo.AreaCountCacheState SET Status = 'Building';
UPDATE dbo.LocationCountCacheState SET Status = 'Building';
TRUNCATE TABLE dbo.AreaCountCacheLevel1;
TRUNCATE TABLE dbo.AreaCountCacheLevel2;
TRUNCATE TABLE dbo.LocationCountCacheLevel1;");

        migrationBuilder.DropPrimaryKey(
            name: "PK_LocationCountCacheLevel1",
            table: "LocationCountCacheLevel1");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AreaCountCacheLevel2",
            table: "AreaCountCacheLevel2");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AreaCountCacheLevel1",
            table: "AreaCountCacheLevel1");

        migrationBuilder.AddColumn<byte>(
            name: "Mask",
            table: "LocationCountCacheLevel1",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<byte>(
            name: "Mask",
            table: "AreaCountCacheLevel2",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<byte>(
            name: "Mask",
            table: "AreaCountCacheLevel1",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddPrimaryKey(
            name: "PK_LocationCountCacheLevel1",
            table: "LocationCountCacheLevel1",
            columns: new[] { "DimensionId", "BucketId", "East", "North", "LocationId", "Mask" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AreaCountCacheLevel2",
            table: "AreaCountCacheLevel2",
            columns: new[] { "DimensionPairId", "BucketA", "BucketB", "EntityTypeId", "EntityId", "Mask" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AreaCountCacheLevel1",
            table: "AreaCountCacheLevel1",
            columns: new[] { "DimensionId", "BucketId", "EntityTypeId", "EntityId", "Mask" });

        foreach (var tabell in Tabeller)
        {
            migrationBuilder.Sql(
                $"ALTER TABLE dbo.{tabell} REBUILD WITH (DATA_COMPRESSION = PAGE);");
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE dbo.AreaCountCacheState SET Status = 'Building';
UPDATE dbo.LocationCountCacheState SET Status = 'Building';
TRUNCATE TABLE dbo.AreaCountCacheLevel1;
TRUNCATE TABLE dbo.AreaCountCacheLevel2;
TRUNCATE TABLE dbo.LocationCountCacheLevel1;");

        migrationBuilder.DropPrimaryKey(
            name: "PK_LocationCountCacheLevel1",
            table: "LocationCountCacheLevel1");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AreaCountCacheLevel2",
            table: "AreaCountCacheLevel2");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AreaCountCacheLevel1",
            table: "AreaCountCacheLevel1");

        migrationBuilder.DropColumn(name: "Mask", table: "LocationCountCacheLevel1");
        migrationBuilder.DropColumn(name: "Mask", table: "AreaCountCacheLevel2");
        migrationBuilder.DropColumn(name: "Mask", table: "AreaCountCacheLevel1");

        migrationBuilder.AddPrimaryKey(
            name: "PK_LocationCountCacheLevel1",
            table: "LocationCountCacheLevel1",
            columns: new[] { "DimensionId", "BucketId", "East", "North", "LocationId" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AreaCountCacheLevel2",
            table: "AreaCountCacheLevel2",
            columns: new[] { "DimensionPairId", "BucketA", "BucketB", "EntityTypeId", "EntityId" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AreaCountCacheLevel1",
            table: "AreaCountCacheLevel1",
            columns: new[] { "DimensionId", "BucketId", "EntityTypeId", "EntityId" });

        foreach (var tabell in Tabeller)
        {
            migrationBuilder.Sql(
                $"ALTER TABLE dbo.{tabell} REBUILD WITH (DATA_COMPRESSION = PAGE);");
        }
    }
}

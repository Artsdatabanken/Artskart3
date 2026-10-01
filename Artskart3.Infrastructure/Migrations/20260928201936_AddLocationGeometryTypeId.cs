using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Geometritypen materialisert på Location, med typen som ledende kolonne i en
/// egen indeks.
///
/// HVORFOR
/// Polygonhentingen filtrerte med Geometry.STGeometryType(), som ikke kan
/// pushes ned i en indeks. Funksjonen ble kalt på hver rad som overlevde
/// utsnittsfilteret — 967 649 for Oslo-utsnittet — for å finne de 10 902 med
/// polygon. Målt:
///
///   bare utsnitt          175 ms
///   + STGeometryType()    951 ms
///   + persistert kolonne   37 ms
///
/// Hele polygonspørringen gikk fra 1010 til 92 ms.
///
/// IKKE EN FILTRERT INDEKS
/// Det var første forsøk, men SQL Server tillater ikke at et filtrert
/// indeksfilter refererer en beregnet kolonne. Integrasjonstestene fanget det.
/// Med typen som ledende nøkkelkolonne blir oppslaget et søk på likhet (2 eller
/// 3) etterfulgt av utsnittsintervallet, og de 5 024 795 punktlokasjonene
/// berøres aldri.
///
/// EN TINYINT, IKKE FLERE BOOLSKE FLAGG
/// Typene er gjensidig utelukkende, så flagg kunne kommet i utakt med
/// hverandre. Og skal linjelokasjoner en dag vises — 2 101 lokasjoner med
/// 62 764 observasjoner faller i dag stille ut — er det en endring i en
/// IN-liste, ikke en ny kolonne og en ny migrasjon.
///
/// Uttrykket speiler LocationGeometryType, og de to må endres sammen:
/// databasen kjenner ikke enumen. Kilden er
/// ArtskartDbContext.LocationGeometryTypeSql.
/// </summary>
public partial class AddLocationGeometryTypeId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "GeometryTypeId",
            table: "Location",
            type: "tinyint",
            nullable: false,
            computedColumnSql: "CAST(CASE [Geometry].STGeometryType()\n    WHEN 'Point'              THEN 1\n    WHEN 'Polygon'            THEN 2\n    WHEN 'MultiPolygon'       THEN 3\n    WHEN 'LineString'         THEN 4\n    WHEN 'MultiLineString'    THEN 5\n    WHEN 'GeometryCollection' THEN 6\n    ELSE 0\nEND AS TINYINT)",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "IX_Location_GeometryTypeEastNorth",
            table: "Location",
            columns: new[] { "GeometryTypeId", "East", "North" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Location_GeometryTypeEastNorth",
            table: "Location");

        migrationBuilder.DropColumn(
            name: "GeometryTypeId",
            table: "Location");
    }
}

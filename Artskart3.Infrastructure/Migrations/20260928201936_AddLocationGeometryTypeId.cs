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
    /// <remarks>
    /// UTENFOR MIGRASJONSTRANSAKSJONEN, OG IDEMPOTENT — som de andre tunge
    /// migrasjonene på denne grenen.
    ///
    /// En PERSISTERT beregnet kolonne skriver om HVER rad i Location: 5,1 millioner
    /// rader, og den etterlot tabellen 95,4 % fragmentert (se
    /// DatabaseMaintenanceJob). Ligger arbeidet i migrasjonstransaksjonen, holdes
    /// tabellen under en skjemalås hele veien, og en timeout ruller tilbake både
    /// omskrivingen OG indeksen — altså alt, etter å ha betalt for alt.
    ///
    /// Delt i to setninger med egne vakter i stedet: feiler indeksen, står kolonnen
    /// igjen, og en ny kjøring hopper over den og tar bare indeksen.
    ///
    /// Uttrykket er duplisert fra ArtskartDbContext.LocationGeometryTypeSql med
    /// vilje — en migrasjon skal beskrive skjemaet slik det var da den ble skrevet,
    /// og ikke endre seg fordi en konstant i koden endres senere.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('dbo.Location', 'GeometryTypeId') IS NULL
            BEGIN
                RAISERROR('Legger til Location.GeometryTypeId (omskriver 5,1 mill. rader)...', 0, 1) WITH NOWAIT;

                ALTER TABLE dbo.Location ADD GeometryTypeId AS CAST(CASE [Geometry].STGeometryType()
                    WHEN 'Point'              THEN 1
                    WHEN 'Polygon'            THEN 2
                    WHEN 'MultiPolygon'       THEN 3
                    WHEN 'LineString'         THEN 4
                    WHEN 'MultiLineString'    THEN 5
                    WHEN 'GeometryCollection' THEN 6
                    ELSE 0
                END AS TINYINT) PERSISTED NOT NULL;
            END
            """, suppressTransaction: true);

        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_Location_GeometryTypeEastNorth'
                             AND object_id = OBJECT_ID('dbo.Location'))
            BEGIN
                RAISERROR('Oppretter IX_Location_GeometryTypeEastNorth...', 0, 1) WITH NOWAIT;

                CREATE NONCLUSTERED INDEX IX_Location_GeometryTypeEastNorth
                ON dbo.Location (GeometryTypeId, East, North);
            END

            -- MERK: omskrivingen over etterlater Location kraftig fragmentert - 95,4 %
            -- maalt. En ALTER INDEX ALL ON dbo.Location REBUILD WITH (MAXDOP = 4) boer
            -- kjoeres etter denne migrasjonen. Den staar IKKE her: den tar minutter,
            -- og en betinget sjekk paa fragmenteringsgraden ville krevd
            -- VIEW DATABASE STATE, altsaa gjort migrasjonen avhengig av en rettighet
            -- den ellers ikke trenger. Se DatabaseMaintenanceJob.
            """, suppressTransaction: true);
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

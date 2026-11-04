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
    /// rader, og det etterlater tabellen 95,4 % fragmentert (målt, se
    /// DatabaseMaintenanceJob). Ligger arbeidet i migrasjonstransaksjonen, holdes
    /// tabellen under en skjemalås hele veien, og en timeout ruller tilbake både
    /// omskrivingen OG indeksen — altså alt, etter å ha betalt for alt.
    ///
    /// Delt i tre setninger med egne vakter i stedet: feiler indeksen, står kolonnen
    /// igjen, og en ny kjøring hopper over den og tar bare indeksen.
    ///
    /// Tredje setning rydder fragmenteringen omskrivingen lager. Den er ubetinget og
    /// har ingen vakt — en REBUILD som ikke trengs koster tid, ikke riktighet — og
    /// den står sist fordi den skal rydde etter alt det andre. Dataene er korrekte
    /// uten den; det er lagringen som er oppblåst, og den blir ikke bedre av seg selv.
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

            """, suppressTransaction: true);

        // --- Rydd opp etter omskrivingen -------------------------------------
        //
        // Den persisterte kolonnen over skriver om hver av de 5,1 millioner radene,
        // radene vokser, sidene splittes - og tabellen staar igjen 95,4 % fragmentert
        // (maalt, se DatabaseMaintenanceJob). Dataene er riktige; det er LAGRINGEN som
        // er oppblaast, og SQL Server betaler i sider. Til sammenligning gikk
        // PK_dbo.Observation fra 99,2 % og 16,0 GB til 0 % og 8,0 GB av en REBUILD:
        // halvparten var luft som likevel ble lest fra disk.
        //
        // DETTE RYDDES IKKE AV SEG SELV. SQL Server har ingen bakgrunnsjobb som
        // defragmenterer; en tabell som er 95,4 % fragmentert blir det til noen
        // kjoerer REBUILD. Kommentaren her pekte tidligere paa DatabaseMaintenanceJob,
        // men den er ikke implementert - og ikke registrert noe sted - saa i praksis
        // ville ingenting kjoert den. Da hoerer den her, i migrasjonen som laget
        // rotet: 538 -> 504 s paa Standard-nivaaet i ytelsessuiten, og en
        // Locations-"regresjon" paa 23 % som viste seg aa vaere fragmentering og ikke
        // kode, er prisen for aa utsette den.
        //
        // UBETINGET, UTEN FRAGMENTERINGSSJEKK. En betinget sjekk ville krevd
        // sys.dm_db_index_physical_stats og dermed VIEW DATABASE STATE - en rettighet
        // migrasjonen ellers ikke trenger, og som en least-privilege-paalogging ikke
        // har. En REBUILD som ikke trengs koster tid, ikke riktighet, og paa en fersk
        // database er den momentan fordi det ikke finnes sider aa flytte.
        //
        // SIST I Up(): en nyopprettet indeks er tett i utgangspunktet, saa
        // IX_Location_GeometryTypeEastNorth over trenger ingen rebuild - det er
        // klyngeindeksen og de oevrige som skal ryddes, og de skal ryddes etter at
        // alt annet har skrevet.
        //
        // MAXDOP = 4 som anbefalt i DatabaseMaintenanceJob: rask nok, uten aa legge
        // beslag paa hele maskinen midt i en deploy. Tabellen er laast mens det staar
        // paa - REBUILD er offline utenfor Enterprise - men migrasjonen holder
        // allerede skjemalaas paa Location i omskrivingen over, saa vinduet finnes
        // uansett.
        //
        // Kjoerer IKKE der migrasjonen alt er anvendt. Artskart3IndexProdLikeTestMigrations
        // ble ryddet manuelt 29.09.2026, og er maalt til 0 % paa samtlige indekser paa
        // Location, saa det er ingen luke der.
        migrationBuilder.Sql("""
            RAISERROR('Bygger indeksene paa dbo.Location paa nytt etter omskrivingen...', 0, 1) WITH NOWAIT;

            ALTER INDEX ALL ON dbo.Location REBUILD WITH (MAXDOP = 4);
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

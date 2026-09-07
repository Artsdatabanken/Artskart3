using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Oppretter columnstore-indeksen på ObservationEntityIndex.
///
/// Egen migrasjon fordi den skiller seg fra alt annet i skjemaendringen foran:
/// den tar 10-30 minutter på 192M rader, og må kjøre utenfor migrasjons-
/// transaksjonen. En så lang operasjon inne i en transaksjon hindrer avkorting
/// av transaksjonsloggen mens den pågår, noe som på Azure SQL (alltid full
/// recovery) kan blåse opp loggen — og en feil ville gitt en tilsvarende lang
/// rollback.
///
/// HVORFOR COLUMNSTORE:
/// Områdetellinger er scan + filter + COUNT over hele tabellen gruppert på område.
/// De lavselektive filtrene (kategori har 15 verdier, taksongruppe 55, funntype 8)
/// treffer mange millioner rader hver, så et indeks-seek hjelper ikke — jobben er
/// selve aggregeringen. Columnstore gir batch-mode (~900 rader per operasjon),
/// aggregate pushdown og segment elimination.
///
/// Målt effekt på 192M rader:
///   Kategori alene    4-5 s -> ~450 ms
///   Orden (Diptera)   "svært tregt" -> ~300 ms
///   Størrelse         192M rader komprimert til ~1,3 GB
///
/// ObservationId er med i kolonnelisten med vilje. Kolonnen har høy kardinalitet,
/// som normalt komprimerer dårlig, men tabellen er clustered på ObservationId —
/// radene ligger derfor fysisk sortert, og hver rowgroup dekker et sammenhengende
/// ID-intervall som delta-encoding håndterer godt. Uten kolonnen kunne ikke
/// spørringer som joiner mot Observation betjenes herfra.
///
/// REKKEFØLGE VED NYOPPSETT:
/// Med Database:AutoMigrate kjører denne ved oppstart, før backfillen. Backfill-
/// skriptet deaktiverer så indeksen og bygger den opp igjen, så den bygges to
/// ganger. Vil man unngå det, kan man deployere fram til forrige migrasjon,
/// kjøre Scripts/BackfillAll.sql, og deretter deployere denne.
/// </summary>
public partial class AddObservationEntityIndexColumnstore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_OEI_Columnstore
ON dbo.ObservationEntityIndex
    (ObservationId, EntityTypeId, EntityId,
     TaxonGroupId, CategoryId, BasisOfRecordId, RegistrationStatusId,
     HasMediaFiles, DateTimeCollected, CoordinatePrecisionInMeters,
     SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId)
WITH (MAXDOP = 4);
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex;",
            suppressTransaction: true);
    }
}

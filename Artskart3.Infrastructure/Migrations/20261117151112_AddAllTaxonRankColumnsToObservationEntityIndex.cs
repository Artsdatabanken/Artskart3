using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// De 19 taksonrangkolonnene som manglet på ObservationEntityIndex. Tabellen hadde
/// art, slekt, familie og orden; nå ligger alle 23 rangene som forekommer som
/// synlige noder i artstreet der.
///
/// HVORFOR
/// To ting fulgte av at bare fire ranger var denormalisert:
///
/// 1. Taksonfilteret hadde TO kodestier. ApplyTaxonFilterToEntityIndex brukte
///    kolonnen når rangen var 11, 15, 19 eller 22, og falt ellers tilbake til
///    ObservationTaxonHierarchy. To stier for samme filter er en kilde til at de
///    en dag svarer ulikt.
/// 2. Artstreet kunne ikke vise filtrerte tall for 9 % av nodene uten et
///    sekundoppslag per utvidelse. Målt: fra indekskolonne 334-426 ms, fra
///    hierarkitabellen ~2 s.
///
/// MÅLT KOSTNAD
/// Kolonnene legges til som NULLABLE uten default, så selve ALTER-en er en
/// metadataoperasjon — den skriver ingen rader. Kostnaden kommer av å fylle dem:
///
///   rowstore, sidekomprimert    +2,37 GB  (målt på 10 %-prøve: 400,9 → 643,9 MB)
///   columnstore                 +65 MB    (målt: 107,0 → 113,5 MB på samme prøve)
///
/// Columnstore-tillegget er lite fordi kolonnene enten er svært repetitive
/// (272 klasser over 61M rader) eller nesten tomme (varietet 0,1 %, underart 1,6 %).
///
/// 2,2x-REDUNDANSEN ER BEVISST. Tabellen har ~2,2 rader per observasjon, én per
/// områdetype, så hver rangkolonne lagres 2,2 ganger. Det er prisen for at filteret
/// og grupperingen ligger på samme rad som områdetilknytningen. Alternativet —
/// columnstore på ObservationTaxonHierarchy — kostet bare 273 MB, men lot 9 % av
/// treutvidelsene ligge på ~2 s.
///
/// ObservationTaxonHierarchy BEHOLDES. Den har én rad per observasjon og bærer
/// taksonfilteret mot Observation-tabellen, der et seek på IX_OTH_* er målt 4,4x
/// raskere enn DISTINCT over indekstabellen (2 583 mot 11 491 ms).
///
/// ETTER DENNE MÅ BACKFILLEN KJØRES
/// Kolonnene står NULL til BackfillAll seksjon D har fylt dem, og den fyller nå alle
/// 23. Seksjonen har fått nytt vannmerke, så den kjører om igjen i miljøer der den
/// alt var ferdig. Backfillen slipper IX_OEI_Columnstore ved start og bygger den
/// til slutt med full kolonneliste — der er de 19 lagt til.
///
/// Fram til backfillen er kjørt svarer taksonfilteret som før: koden leser kolonnen
/// bare for ranger den vet er fylt.
/// </summary>
public partial class AddAllTaxonRankColumnsToObservationEntityIndex : Migration
{
    /// <summary>
    /// Rangene i hierarkisk rekkefølge. Rekkefølgen her betyr ingenting for
    /// databasen, men gjør listen lesbar mot TaxonRank-tabellen.
    /// </summary>
    private static readonly string[] Columns =
    [
        "KingdomTaxonId",
        "SubkingdomTaxonId",
        "PhylumTaxonId",
        "SubphylumTaxonId",
        "SuperclassTaxonId",
        "ClassTaxonId",
        "SubclassTaxonId",
        "InfraclassTaxonId",
        "SuperorderTaxonId",
        "SuborderTaxonId",
        "InfraorderTaxonId",
        "SuperfamilyTaxonId",
        "SubfamilyTaxonId",
        "TribeTaxonId",
        "SubgenusTaxonId",
        "SectionTaxonId",
        "SubspeciesTaxonId",
        "VarietyTaxonId",
        "FormTaxonId",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.AddColumn<int>(
                name: column,
                table: "ObservationEntityIndex",
                type: "int",
                nullable: true);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropColumn(name: column, table: "ObservationEntityIndex");
        }
    }
}

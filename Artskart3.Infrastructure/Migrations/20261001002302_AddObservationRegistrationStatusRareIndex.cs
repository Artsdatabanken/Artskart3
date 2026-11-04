using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Filtrert indeks for de to sjeldne registreringsstatusene.
///
/// HVA DEN LØSER
/// Listevisningen sorterer på DateTimeCollected og pagineres med OFFSET/FETCH.
/// Arbeidet er da antall rader som trengs delt på selektiviteten, og med 125
/// treff i 61 millioner skanner selv side 1 nesten hele datoindeksen. Målt:
///
///   bilder:lett + regstatus:lett, side 50   3 859 → 27 ms
///   atferd:lett + regstatus:lett, side 1    2 981 → 28 ms
///   atferd:lett + regstatus:lett, side1-stor 2 004 → 28 ms
///   bilder:lett + regstatus:lett, side1-stor 1 725 → 30 ms
///
/// HVORFOR FILTRERT
/// Et vanlig (RegistrationStatusId, DateTimeCollected)-indeks ble bygget og
/// målt: 562 MB, og optimizeren valgte det ALDRI for kombinasjonene. Den
/// anslår 49 485 treff der det er 125 — exponential backoff antar delvis
/// korrelasjon, mens den her er sterkt negativ. Flerkolonnestatistikk med
/// FULLSCAN gjorde anslaget verre (193 510), og DISABLE_OPTIMIZER_ROWGOAL
/// endret ingenting. Se ArtskartDbContext for hele kjeden.
///
/// STØRRELSE
/// 2,1 MB, tre sekunder å bygge. Fordelingen er ekstrem: verdi 1 er 99,67 %,
/// verdi 2 er 0,32 %, verdi 3 er 0,01 %. Verdi 1 er rask uten og kan uansett
/// ikke filtreres nyttig.
///
/// QUOTED_IDENTIFIER
/// Filtrerte indekser krever SET QUOTED_IDENTIFIER ON ved både lesing og
/// skriving. .NET SqlClient har den på. En INSERT eller UPDATE mot Observation
/// fra en forbindelse som har den AV vil feile — det gjelder importjobben når
/// den skrives. Prosjektet har allerede filtrerte indekser på
/// ObservationTaxonHierarchy, så mønsteret er etablert.
/// </summary>
public partial class AddObservationRegistrationStatusRareIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Observation_RegistrationStatusRare",
            table: "Observation",
            columns: new[] { "RegistrationStatusId", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[RegistrationStatusId] IN (2, 3)")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        // Sidekomprimering — EF Core har ingen parameter for DATA_COMPRESSION.
        // Samme mønster som IX_Observation_CatalogNumber og bufferne.
        migrationBuilder.Sql(
            "ALTER INDEX IX_Observation_RegistrationStatusRare ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_RegistrationStatusRare",
            table: "Observation");
    }
}

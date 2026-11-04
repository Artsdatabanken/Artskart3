using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Gir de fire taggfiltrerte indeksene samme INCLUDE som
/// IX_Observation_RegistrationStatusRare har hatt hele tiden.
///
/// HVA DET LØSER
/// Indeksene fant de merkede radene i datorekkefølge, men måtte slå opp i
/// klyngeindeksen per rad så snart filteret også nevnte atferd, bilder eller
/// koordpresisjon. Det er nøyaktig de tre kolonnene som ikke har sin egen
/// (kolonne, DateTimeCollected)-indeks — de sju andre dimensjonene har en, og
/// kombinasjoner med dem kunne bruke den.
///
/// Målt på listevisningen i Full-kjøringen 20.10, mot en median på 22 ms for
/// endepunktet ellers:
///
///   annotering:lett + atferd:lett                              1 908 ms
///   atferd:lett + koordpresisjon:lett + opprinnelse:lett          539 ms
///   annotering:lett + artsbestemmelse:lett + sensitivitet:lett    494 ms
///   atferd:lett + koordpresisjon:lett                             293 ms
///
/// Samme klasse som tallene RegistrationStatusRare ble laget for (3 859 ms og
/// 2 981 ms). Mønsteret ble kopiert da de fire indeksene kom til, men INCLUDE
/// ble ikke med.
///
/// BILLIG
/// Indeksene dekker 2 194 til 133 357 rader av 61 millioner og er på 0,1-5,5 MB.
/// Dette er ikke en tabellomskriving — bare fire små indekser bygget på nytt.
///
/// SIDEKOMPRIMERING MÅ SETTES PÅ NYTT
/// DROP + CREATE mister DATA_COMPRESSION fra migrasjonene som opprettet dem.
/// EF Core har ingen parameter for det, så den settes med rå SQL som før.
///
/// Statistikk trengs ikke oppdateres eksplisitt her: CREATE INDEX bygger
/// statistikken med full skanning av indeksen.
/// </summary>
public partial class AddIncludeColumnsToTagFilteredIndexes : Migration
{
    private static readonly string[] Indekser =
    [
        "IX_Observation_ChangedByAnnotation",
        "IX_Observation_Sensitive",
        "IX_Observation_UncertainIdentification",
        "IX_Observation_Introduced",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_Introduced",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation",
            columns: new[] { "ChangedByAnnotation", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[ChangedByAnnotation] = 1")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Introduced",
            table: "Observation",
            columns: new[] { "Introduced", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Introduced] = 1")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation",
            columns: new[] { "Sensitive", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Sensitive] = 1")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        migrationBuilder.CreateIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation",
            columns: new[] { "UncertainIdentification", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[UncertainIdentification] = 1")
            .Annotation("SqlServer:Include", new[] { "HasMediaFiles", "BehaviorId", "CoordinatePrecisionInMeters" });

        foreach (var indeks in Indekser)
        {
            migrationBuilder.Sql(
                $"ALTER INDEX {indeks} ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_Introduced",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation");

        migrationBuilder.DropIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_ChangedByAnnotation",
            table: "Observation",
            columns: new[] { "ChangedByAnnotation", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[ChangedByAnnotation] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Introduced",
            table: "Observation",
            columns: new[] { "Introduced", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Introduced] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_Sensitive",
            table: "Observation",
            columns: new[] { "Sensitive", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[Sensitive] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_Observation_UncertainIdentification",
            table: "Observation",
            columns: new[] { "UncertainIdentification", "DateTimeCollected", "Id" },
            descending: new[] { false, true, true },
            filter: "[UncertainIdentification] = 1");

        foreach (var indeks in Indekser)
        {
            migrationBuilder.Sql(
                $"ALTER INDEX {indeks} ON dbo.Observation REBUILD WITH (DATA_COMPRESSION = PAGE);");
        }
    }
}

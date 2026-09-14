using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// CsvExportJob.UserId fra nvarchar(256) til uniqueidentifier.
///
/// Kolonnen holder brukerens «sub», altså samme verdi som Users.Id. Som streng
/// uten formatkrav kunne den også holde fritekst, og det gjorde den i praksis:
/// controlleren falt tilbake på visningsnavnet når sub manglet, slik at jobben
/// ble lagret under «Kari Nordmann». Alle lesestier filtrerer på UserId, så den
/// eksporten var usynlig og umulig å laste ned for brukeren selv. Med
/// uniqueidentifier kan feilen ikke gjenoppstå — typen utelukker den.
///
/// MERK — indeksen må slippes først. SQL Server avviser ALTER COLUMN på en
/// kolonne det finnes en indeks på:
///   Msg 5074: The index 'IX_CsvExportJob_UserId' is dependent on column 'UserId'.
/// EF genererte bare ALTER COLUMN, altså en migrasjon som ville stoppet ved
/// deploy. Drop/opprett rundt er lagt til for hånd og verifisert mot SQL Server
/// 2022.
///
/// Konverteringen forutsetter at alle eksisterende verdier er gyldige GUID-er.
/// Er de ikke det, feiler migrasjonen med «Conversion failed when converting from
/// a character string to uniqueidentifier» og ruller tilbake med dataene i
/// behold — den ødelegger altså ingenting, men den må da ryddes før den kjøres.
/// Sjekk med:
///   SELECT UserId, COUNT(*) FROM CsvExportJob
///   WHERE TRY_CONVERT(uniqueidentifier, UserId) IS NULL GROUP BY UserId;
/// </summary>
/// <inheritdoc />
public partial class ChangeCsvExportJobUserIdToGuid : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CsvExportJob_UserId",
            table: "CsvExportJob");

        migrationBuilder.AlterColumn<Guid>(
            name: "UserId",
            table: "CsvExportJob",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(256)",
            oldMaxLength: 256);

        migrationBuilder.CreateIndex(
            name: "IX_CsvExportJob_UserId",
            table: "CsvExportJob",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CsvExportJob_UserId",
            table: "CsvExportJob");

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "CsvExportJob",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.CreateIndex(
            name: "IX_CsvExportJob_UserId",
            table: "CsvExportJob",
            column: "UserId");
    }
}

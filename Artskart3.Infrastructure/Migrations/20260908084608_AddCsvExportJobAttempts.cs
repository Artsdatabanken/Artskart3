using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Forsøksteller på eksportjobber.
///
/// Uten den var en jobb som drepte workeren (i stedet for å kaste) en
/// evighetsløkke: gjenopprettingen satte den tilbake til Pending med opprinnelig
/// CreatedAt, og claimen plukker eldste ventende jobb — så den var straks først i
/// køen igjen, og ingen andres eksporter kom gjennom.
///
/// Eksisterende rader får 0, som er riktig: de har ikke brukt noen forsøk ennå.
/// </summary>
/// <inheritdoc />
public partial class AddCsvExportJobAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Attempts",
            table: "CsvExportJob",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Attempts",
            table: "CsvExportJob");
    }
}

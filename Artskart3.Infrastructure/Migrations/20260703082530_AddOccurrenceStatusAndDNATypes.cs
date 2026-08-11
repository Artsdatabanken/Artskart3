using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDNATypes : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed new BasisOfRecord entries for DNA types
            migrationBuilder.InsertData(
                table: "BasisOfRecord",
                columns: ["Id", "Name", "Description", "Variants", "CreatedAt", "UpdatedAt", "IsDeleted"],
                values: new object[,]
                {
                    { 16, "dna", "DNA", "dna,DNA", default(DateTime), default(DateTime), false },
                    { 17, "edna", "eDNA", "edna,eDNA,environmental dna,environmental DNA", default(DateTime), default(DateTime), false },
                    { 18, "edna_barcoding", "eDNA Barcoding", "edna_barcoding,eDNA Barcoding,eDAN Barcoding", default(DateTime), default(DateTime), false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "BasisOfRecord",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "BasisOfRecord",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "BasisOfRecord",
                keyColumn: "Id",
                keyValue: 18);
        }
}

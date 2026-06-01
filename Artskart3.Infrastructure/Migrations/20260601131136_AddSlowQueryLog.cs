using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlowQueryLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlowQueryLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Endpoint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    QueryTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    ThresholdMs = table.Column<long>(type: "bigint", nullable: false),
                    RequestPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlowQueryLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlowQueryLog_Endpoint",
                table: "SlowQueryLog",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_SlowQueryLog_OccurredAt",
                table: "SlowQueryLog",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlowQueryLog");
        }
    }
}

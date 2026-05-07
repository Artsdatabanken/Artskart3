using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations.SessionDb
{
    /// <inheritdoc />
    public partial class UserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Renewed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expires = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ticket = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_Expires",
                table: "UserSessions",
                column: "Expires");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_PartitionKey_Key",
                table: "UserSessions",
                columns: new[] { "PartitionKey", "Key" },
                unique: true,
                filter: "[PartitionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_PartitionKey_SessionId",
                table: "UserSessions",
                columns: new[] { "PartitionKey", "SessionId" },
                unique: true,
                filter: "[PartitionKey] IS NOT NULL AND [SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_PartitionKey_SubjectId_SessionId",
                table: "UserSessions",
                columns: new[] { "PartitionKey", "SubjectId", "SessionId" },
                unique: true,
                filter: "[PartitionKey] IS NOT NULL AND [SessionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSessions");
        }
    }
}

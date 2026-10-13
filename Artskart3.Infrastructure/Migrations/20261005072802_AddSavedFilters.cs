using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSavedFilters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavedFilter",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FilterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MinX = table.Column<double>(type: "float", nullable: true),
                MinY = table.Column<double>(type: "float", nullable: true),
                MaxX = table.Column<double>(type: "float", nullable: true),
                MaxY = table.Column<double>(type: "float", nullable: true),
                IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedFilter", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SavedFilter_PublicId",
            table: "SavedFilter",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavedFilter_UserId",
            table: "SavedFilter",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SavedFilter");
    }
}

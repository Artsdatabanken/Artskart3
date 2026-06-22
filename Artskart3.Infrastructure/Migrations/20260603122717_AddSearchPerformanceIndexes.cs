using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSearchPerformanceIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_Area_AreaTypeId_Fid_IsCurrent]
                ON [Area] ([AreaTypeId], [Fid], [IsCurrent])
                INCLUDE ([Id]);
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Area_AreaTypeId_Fid_IsCurrent",
            table: "Area");
    }
}

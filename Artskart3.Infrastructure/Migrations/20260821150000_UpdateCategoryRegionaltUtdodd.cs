using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;
public partial class UpdateCategoryRegionaltUtdodd : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE [dbo].[Category]
                SET Name = N'Regionalt utdødd'
                WHERE Id = 15;
            ");
    }
}

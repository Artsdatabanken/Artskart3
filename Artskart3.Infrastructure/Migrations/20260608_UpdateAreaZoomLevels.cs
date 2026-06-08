using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAreaZoomLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Artskart3Index].[dbo].[Area] 
                SET ZoomLevel = 1 
                WHERE AreaTypeID = 2;
                
                UPDATE [Artskart3Index].[dbo].[Area] 
                SET ZoomLevel = 2 
                WHERE AreaTypeID = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Artskart3Index].[dbo].[Area] 
                SET ZoomLevel = NULL 
                WHERE AreaTypeID IN (1, 2);
            ");
        }
    }
}

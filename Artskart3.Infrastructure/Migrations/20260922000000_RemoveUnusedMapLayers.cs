using Microsoft.EntityFrameworkCore.Migrations;
using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ArtskartDbContext))]
[Migration("20260922000000_RemoveUnusedMapLayers")]
public partial class RemoveUnusedMapLayers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM [MapLayer] WHERE [Name] IN (N'Matrikkel', N'Bioseksjoner', N'Biosoner');");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}

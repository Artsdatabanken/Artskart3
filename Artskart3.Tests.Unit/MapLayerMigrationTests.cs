using Artskart3.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Artskart3.Tests.Unit;

public class MapLayerMigrationTests
{
    private const string InitialMigration = "20260910075213_AddMapLayer";
    private const string PreviousMigration = "20261104091607_RebuildCountCaches";
    private const string LatestMigration = "20261105150132_UpdateMapLayerTable";

    [Fact]
    public void MapLayerMigrations_GenerateIdempotentUpgradeScript()
    {
        using var context = CreateContext();

        var script = context.GetService<IMigrator>().GenerateScript(
            InitialMigration, LatestMigration, MigrationsSqlGenerationOptions.Idempotent);

        script.Should().Contain("20260914065925_SeedMapLayerGrenser");
        script.Should().Contain("seksjoner2017");
        script.Should().Contain("soner2017");
        script.Should().Contain("kd_administrative_grenser,adm_grenser");
        script.Should().Contain("eiendomsgrense,eiendoms_id");
        script.Should().Contain("Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn");
        script.Should().Contain("WHEN N'Eiendomskart' THEN 10");
        script.Should().Contain("WHEN N'Biosoner' THEN 50");
        script.Should().Contain(LatestMigration);
        script.Should().NotContain("20261006000000_SeedMatrikkelMapLayers");
    }

    [Fact]
    public void UpdateMapLayerTableMigration_SeedsMaritimeLayerOnlyIfMissingAndRemovesItOnRollback()
    {
        using var context = CreateContext();

        var upgradeScript = context.GetService<IMigrator>().GenerateScript(PreviousMigration, LatestMigration);
        var rollbackScript = context.GetService<IMigrator>().GenerateScript(LatestMigration, PreviousMigration);

        upgradeScript.Should().Contain("IF NOT EXISTS (SELECT 1 FROM [MapLayer] WHERE [Name] = N'MaritimeGrenser')");
        upgradeScript.Should().Contain("Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn");
        rollbackScript.Should().Contain("Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn");
        rollbackScript.Should().Contain("DROP COLUMN [UseSingleImage]");
    }

    [Fact]
    public void UpdateMapLayerTableMigration_ConfiguresExistingLayersWithoutUnrelatedSchemaChanges()
    {
        using var context = CreateContext();

        var upgradeScript = context.GetService<IMigrator>().GenerateScript(PreviousMigration, LatestMigration);

        upgradeScript.Should().Contain("[Opacity] = 0.5");
        upgradeScript.Should().Contain("[MaxScaleDenominator] = 100000");
        upgradeScript.Should().Contain("[RetryAfterSeconds] = 30");
        upgradeScript.Should().Contain("[UseSingleImage] = 1");
        upgradeScript.Should().NotContain("CREATE TABLE [SavedFilter]");
        upgradeScript.Should().NotContain("ALTER COLUMN [SortOrder]");
    }

    [Fact]
    public void MapLayerMigrations_GenerateRollbackScript()
    {
        using var context = CreateContext();

        var script = context.GetService<IMigrator>().GenerateScript(LatestMigration, InitialMigration);

        script.Should().Contain("DROP COLUMN [SortOrder]");
        script.Should().Contain("DELETE FROM [MapLayer] WHERE [Name] = N'Eiendomskart'");
        script.Should().Contain("MaritimeGrenser");
        script.Should().Contain("Maritime_omraader,Maritime_grenser,Maritime_omraader_navn,Maritime_grenser_navn");
        script.Should().Contain("Bioseksjoner");
        script.Should().Contain("Biosoner");
    }

    private static ArtskartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer("Server=localhost;Database=MigrationScriptTests;Integrated Security=True", sql => sql.UseNetTopologySuite())
            .Options;

        return new ArtskartDbContext(options);
    }
}

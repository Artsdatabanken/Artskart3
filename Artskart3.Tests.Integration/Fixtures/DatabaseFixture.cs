using Artskart3.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace Artskart3.Tests.Integration.Fixtures;

/// <summary>
/// Shared fixture that starts one SQL Server container for the entire test collection.
/// Applies EF migrations and loads seed data once, then uses Respawn to reset
/// between test classes without recreating the container.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await ApplyMigrationsAsync();
        await LoadSeedDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var context = new ArtskartDbContext(options);

        // InitialBaseline is intentionally empty (existing DB baseline), so MigrateAsync
        // won't create any tables in a fresh container. EnsureCreatedAsync creates the
        // full schema from the current model, which is correct for test databases.
        await context.Database.EnsureCreatedAsync();
    }

    private async Task LoadSeedDataAsync()
    {
        var seedFile = Path.Combine(
            AppContext.BaseDirectory, "SeedData", "seed_data.sql");

        if (!File.Exists(seedFile))
        {
            // No seed file yet — tests that require data will skip or use empty DB.
            // Generate seed_data.sql by running extract_seed_data.sql against
            // production/staging and placing the output in SeedData/seed_data.sql.
            return;
        }

        var sql = await File.ReadAllTextAsync(seedFile);

        // Split on semicolons to execute one statement at a time.
        // All statements run on a single open connection so SET IDENTITY_INSERT
        // and other session-level settings persist across statements.
        var statements = sql
            .Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var stmt in statements)
        {
            try
            {
                await using var cmd = new SqlCommand(stmt, connection);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Seed SQL failed: {ex.Message}\nStatement (first 500 chars): {stmt[..Math.Min(500, stmt.Length)]}", ex);
            }
        }
    }
}

/// <summary>
/// xUnit collection definition — all tests in this collection share one container.
/// </summary>
[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

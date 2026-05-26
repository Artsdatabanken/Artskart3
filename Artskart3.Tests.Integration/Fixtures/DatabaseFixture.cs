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
/// Delt fixture som starter én SQL Server-container for hele testsamlingen.
/// Kjører EF-migrasjoner og laster testdata én gang, deretter brukes Respawn for å nullstille
/// mellom testklasser uten å gjenskape containeren.
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

        // InitialBaseline er bevisst tom (eksisterende DB-grunnlag), så MigrateAsync
        // vil ikke opprette tabeller i en ny container. EnsureCreatedAsync oppretter
        // fullstendig skjema fra gjeldende modell, noe som er riktig for testdatabaser.
        await context.Database.EnsureCreatedAsync();
    }

    private async Task LoadSeedDataAsync()
    {
        var seedFile = Path.Combine(
            AppContext.BaseDirectory, "SeedData", "seed_data.sql");

        if (!File.Exists(seedFile))
        {
            // Ingen testdatafil ennå — tester som krever data vil hoppe over eller bruke tom database.
            // Generer seed_data.sql ved å kjøre extract_seed_data.sql mot
            // produksjon/staging og plassere resultatet i SeedData/seed_data.sql.
            return;
        }

        var sql = await File.ReadAllTextAsync(seedFile);

        // Del på semikolon for å kjøre én setning om gangen.
        // Alle setninger kjøres på én åpen tilkobling slik at SET IDENTITY_INSERT
        // og andre sesjonsnivå-innstillinger beholdes mellom setningene.
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
/// xUnit samlingsdefinisjon — alle tester i denne samlingen deler én container.
/// </summary>
[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

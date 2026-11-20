using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Artskart3.Infrastructure.Services;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Lagringen går mot en ekte SQL Server: den bruker SqlBulkCopy og egen tilkobling, som
/// InMemory ikke har.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class LocationListStoreIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _db;
    private ArtskartDbContext _context = null!;

    public LocationListStoreIntegrationTests(DatabaseFixture db) => _db = db;

    public Task InitializeAsync()
    {
        _context = CreateContext(_db.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task SaveInBackground_ACopyOfTheListFindsTheStoredList_ADifferentListGoesAsJson()
    {
        // Usortert, som listene fra oppslaget, og langt unna ekte lokasjons-ID-er.
        var ids = Enumerable.Range(900_000_000, LocationListStore.MinStoredLength + 500).Reverse().ToArray();
        new LocationListStore(_context, NullLogger.Instance).SaveInBackground(ids);

        int[] kopi = [.. ids.Reverse()];
        (await VenterPaaTabellenAsync(kopi)).Should().BeTrue("en kopi av listen har samme innhold");

        int[] annen = [.. kopi.Skip(1)];
        SqlFor(annen).Should().NotContain("LocationListEntries");

        // Kanten til et oppdelt egendefinert område bæres som matrikkelenhetene, og skal
        // også joine den lagrede listen. Telling av lokasjoner ber om JSON.
        var kant = new LocationSelection(kopi, null);
        kant.Apply(_context, _context.Set<Observation>()).ToQueryString().Should().Contain("LocationListEntries");
        kant.Apply(_context, _context.Set<ObservationEntityIndex>()).ToQueryString().Should().Contain("LocationListEntries");
        kant.Apply(_context, _context.Set<ObservationEntityIndex>(), useStoredList: false).ToQueryString()
            .Should().NotContain("LocationListEntries");
    }

    [Fact]
    public async Task SaveInBackground_LogsAFailedSave_AndTheListGoesAsJson()
    {
        var feil = new SqlConnectionStringBuilder(_db.ConnectionString)
        {
            InitialCatalog = "ArtskartFinnesIkke",
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
        };
        await using var feilContext = CreateContext(feil.ConnectionString);
        var logger = new LoggerSomHusker();

        var ids = Enumerable.Range(910_000_000, LocationListStore.MinStoredLength + 500).ToArray();
        new LocationListStore(feilContext, logger).SaveInBackground(ids);

        for (var i = 0; i < 60 && logger.Advarsler.Count == 0; i++)
            await Task.Delay(500);

        logger.Advarsler.Should().ContainSingle().Which.Should().NotBeNull("feilen skal logges med unntaket");
        SqlFor(ids).Should().NotContain("LocationListEntries");
    }

    [Fact]
    public async Task SaveInBackground_OverTheCap_DoesNotStoreTheList_AndLogsIt()
    {
        var logger = new LoggerSomHusker();
        var ids = Enumerable.Range(920_000_000, LocationListStore.MinStoredLength + 500).ToArray();

        new LocationListStore(_context, logger, maxStoredRows: 0).SaveInBackground(ids);
        await LocationListStore.WaitForSaveAsync(ids, CancellationToken.None);
        for (var i = 0; i < 60 && logger.Advarsler.Count == 0; i++)
            await Task.Delay(500);

        logger.Advarsler.Should().ContainSingle().Which.Should().BeNull("taket er ingen feil");
        SqlFor(ids).Should().NotContain("LocationListEntries");
    }

    /// <summary>
    /// Ved taket ryddes ubrukte lister med en gang, og listen lagres hvis det ble plass, i stedet
    /// for å gå som JSON til ryddingen kommer av seg selv.
    /// </summary>
    [Fact]
    public async Task SaveInBackground_AtTheCap_CleansUpUnusedListsAndStoresTheList()
    {
        var ubrukt = new LocationList
        {
            Hash = RandomNumberGenerator.GetBytes(32),
            LocationCount = 1_000_000,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastUsedAt = DateTime.UtcNow.AddDays(-1),
        };
        _context.Set<LocationList>().Add(ubrukt);
        await _context.SaveChangesAsync();
        var ids = Enumerable.Range(930_000_000, LocationListStore.MinStoredLength + 500).ToArray();
        var iBruk = await _context.Set<LocationList>().Where(l => l.Id != ubrukt.Id).SumAsync(l => (long)l.LocationCount);

        new LocationListStore(_context, NullLogger.Instance, maxStoredRows: iBruk + ids.Length).SaveInBackground(ids);

        (await VenterPaaTabellenAsync(ids)).Should().BeTrue("ryddingen gjorde plass til listen");
        (await _context.Set<LocationList>().AsNoTracking().AnyAsync(l => l.Id == ubrukt.Id)).Should().BeFalse();
    }

    /// <summary>
    /// En spørring bygget med en lagret liste oppgir den, så eksporten kan merke den som brukt
    /// mens den kjører, og ryddingen ikke sletter den underveis.
    /// </summary>
    [Fact]
    public async Task ApplyFiltersAsync_ReportsTheStoredList_AndMarkUsedAsyncKeepsItFromTheCleanup()
    {
        const string wkt = "POLYGON ((0 0, 10 0, 10 10, 0 0))";
        var ids = Enumerable.Range(940_000_000, LocationListStore.MinStoredLength + 500).ToArray();
        new LocationListStore(_context, NullLogger.Instance).SaveInBackground(ids);
        (await VenterPaaTabellenAsync(ids)).Should().BeTrue();

        var storedListIds = new List<int>();
        await ObservationQueryBuilder.ApplyFiltersAsync(
            _context, new StubTaxonHierarchyService(), new StubCadastralUnitService(),
            new StubCustomAreaService(new() { [wkt] = [.. ids] }), _context.Set<Observation>().AsNoTracking(),
            new ObservationSearchFilterDto { CustomAreaWkt = wkt }, storedListIds, CancellationToken.None);

        var listId = storedListIds.Should().ContainSingle().Subject;
        await _context.Set<LocationList>().Where(l => l.Id == listId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LastUsedAt, DateTime.UtcNow.AddHours(-3)));
        await LocationListStore.MarkUsedAsync(_context, storedListIds, CancellationToken.None);

        (await _context.Set<LocationList>().AsNoTracking().SingleAsync(l => l.Id == listId)).LastUsedAt
            .Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    private async Task<bool> VenterPaaTabellenAsync(int[] ids)
    {
        for (var i = 0; i < 60; i++)
        {
            if (SqlFor(ids).Contains("LocationListEntries")) return true;
            await Task.Delay(500);
        }
        return false;
    }

    private string SqlFor(int[] ids) =>
        LocationListStore.WhereAtLocations(_context, _context.Set<ObservationEntityIndex>(), ids).ToQueryString();

    private static ArtskartDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(connectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class LoggerSomHusker : ILogger
    {
        public List<Exception?> Advarsler { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                lock (Advarsler) Advarsler.Add(exception);
        }
    }
}

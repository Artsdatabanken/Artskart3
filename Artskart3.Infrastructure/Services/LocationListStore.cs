using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Lagrer store lokasjonslister i databasen, så spørringene joiner en tabell i stedet for å få
/// listen som én JSON-parameter. Målt rundt Oslo med 277 000 lokasjoner, SQL-tiden alene:
/// LocationCount 0,97 til 0,12-0,19 s, AreaCounts 0,85 til 0,16-0,18 s og Locations 2,0 til
/// 0,6-0,8 s.
///
/// En liste kjennes igjen på innholdet, så like lister deler én rad, også på tvers av
/// instanser. Hvilke lister som er lagret, huskes etter den samme hashen, så filtrene finner
/// tabellen for en hvilken som helst kopi av listen uten at ID-en må følge den gjennom søket.
/// En liste som ikke er lagret ennå, går som JSON, så lagringen kan skje i bakgrunnen uten å
/// forsinke oppslaget.
/// </summary>
public class LocationListStore(IArtsKartDbContext context, ILogger logger, long maxStoredRows = LocationListStore.MaxStoredRows)
{
    // Målt: lister på 11 000-15 000 lokasjoner var like raske som JSON, 57 000 ble 40 % raskere.
    public const int MinStoredLength = 20_000;

    // Listene i minnet lever i høyst en time (LocationIdsCacheDuration), og LastUsedAt settes
    // hver gang en instans lager en av dem, så ingen bruker en rad som er eldre enn det. To
    // timer gir en times margin og holder tabellen liten.
    private static readonly TimeSpan UnusedLifetime = TimeSpan.FromHours(2);

    // Rundt 600 MB, med rundt 30 byte per rad målt lokalt: 33 lister på 600 000 lokasjoner.
    // Polygonene kommer fra hvem som helst, så uten tak kunne tabellen vokse så lenge noen
    // sendte nye. Over taket går nye lister som JSON.
    public const long MaxStoredRows = 20_000_000;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan SaveTimeout = TimeSpan.FromMinutes(5);
    private static long _nextCleanupTicks;
    private static int _leftoversRemoved;

    // Lagringen tok 0,3-0,5 s for 260 000-330 000 lokasjoner. Uten å vente gikk første telling
    // etter tegningen som JSON: målt 2,7-4,0 s for 500 000-600 000 lokasjoner, mot 0,15-0,27 s
    // når listen var lagret. Tar lagringen lenger, går forespørselen som JSON.
    private static readonly TimeSpan MaxSaveWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Slås av med CustomArea:StoreLocationLists=false, se ServiceCollectionExtensions.AddAreaLookupServices.
    /// Da lagres ingen lister, og alle går som JSON.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    private sealed class StoredList(DateTime expiresAt)
    {
        // 0 mens lagringen pågår.
        public int ListId;
        public readonly DateTime ExpiresAt = expiresAt;
        public readonly TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // Etter hashen av innholdet, som i databasen. En oppføring lever like lenge som listene i
    // minnet og settes når raden merkes, så den kan ikke overleve raden: ryddingen sletter bare
    // lister som har stått ubrukt i to timer.
    private static readonly ConcurrentDictionary<string, StoredList> Lists = new();

    // Bare hashen, så den regnes én gang per array.
    private static readonly ConditionalWeakTable<int[], string> Keys = new();

    public static IQueryable<ObservationEntityIndex> WhereAtLocations(
        IArtsKartDbContext context, IQueryable<ObservationEntityIndex> query, int[] locationIds, bool useStoredList = true)
    {
        if (useStoredList && TryGetListId(locationIds, out var listId))
        {
            var entries = Entries(context, listId);
            return query.Where(idx => idx.LocationId != null && entries.Contains(idx.LocationId.Value));
        }

        return query.Where(idx => idx.LocationId != null && EF.Parameter(locationIds).Contains(idx.LocationId.Value));
    }

    public static IQueryable<Observation> WhereAtLocations(
        IArtsKartDbContext context, IQueryable<Observation> query, int[] locationIds)
    {
        if (TryGetListId(locationIds, out var listId))
        {
            var entries = Entries(context, listId);
            return query.Where(o => o.LocationId != null && entries.Contains(o.LocationId.Value));
        }

        return query.Where(o => o.LocationId != null && EF.Parameter(locationIds).Contains(o.LocationId.Value));
    }

    /// <summary>
    /// Lokasjonene som en delspørring mot tabellen hvis listen er lagret, ellers null.
    /// </summary>
    public static IQueryable<int>? StoredLocations(IArtsKartDbContext context, int[] locationIds) =>
        TryGetListId(locationIds, out var listId) ? Entries(context, listId) : null;

    /// <summary>
    /// ID-en til den lagrede listen med samme innhold, eller null når listen går som JSON.
    /// </summary>
    public static int? StoredListId(int[] locationIds) => TryGetListId(locationIds, out var listId) ? listId : null;

    /// <summary>
    /// Merker listene som brukt nå, så ryddingen ikke sletter dem. Ellers regnes en liste som
    /// brukt bare når en instans lager den, og den kan slettes to timer etter det, selv om en
    /// spørring som ble bygget med den, fortsatt kjører.
    /// </summary>
    public static async Task MarkUsedAsync(
        IArtsKartDbContext context, IReadOnlyCollection<int> listIds, CancellationToken cancellationToken)
    {
        if (listIds.Count == 0) return;
        await context.Set<LocationList>()
            .Where(l => listIds.Contains(l.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LastUsedAt, DateTime.UtcNow), cancellationToken);
    }

    private static bool TryGetListId(int[] locationIds, out int listId)
    {
        listId = TryGetStored(locationIds, out var stored) ? Volatile.Read(ref stored.ListId) : 0;
        return listId > 0;
    }

    private static bool TryGetStored(int[] locationIds, out StoredList stored)
    {
        stored = null!;
        return Enabled && locationIds.Length >= MinStoredLength
               && Lists.TryGetValue(KeyFor(locationIds), out stored!) && stored.ExpiresAt > DateTime.UtcNow;
    }

    private static string KeyFor(int[] locationIds) =>
        Keys.GetValue(locationIds, ids => Convert.ToHexString(Hash(SortedDistinct(ids))));

    private static byte[] Hash(int[] sortedDistinct) => SHA256.HashData(MemoryMarshal.AsBytes(sortedDistinct.AsSpan()));

    private static int[] SortedDistinct(int[] ids) =>
        IsSortedDistinct(ids) ? ids : [.. ids.Distinct().Order()];

    private static bool IsSortedDistinct(int[] ids)
    {
        for (var i = 1; i < ids.Length; i++)
            if (ids[i] <= ids[i - 1]) return false;
        return true;
    }

    /// <summary>
    /// Venter til listen er lagret hvis lagringen pågår, men høyst <see cref="MaxSaveWait"/>.
    /// For søkene, ikke for oppslaget som starter lagringen; det skal svare uten den.
    /// </summary>
    public static async Task WaitForSaveAsync(int[]? locationIds, CancellationToken cancellationToken)
    {
        if (locationIds == null || !TryGetStored(locationIds, out var stored) || stored.Done.Task.IsCompleted) return;

        try
        {
            await stored.Done.Task.WaitAsync(MaxSaveWait, cancellationToken);
        }
        catch (TimeoutException)
        {
        }
    }

    private static IQueryable<int> Entries(IArtsKartDbContext context, int listId) =>
        context.Set<LocationListEntry>().Where(e => e.ListId == listId).Select(e => e.LocationId);

    /// <summary>
    /// Lagrer listen i bakgrunnen hvis den er stor nok. Kalles der listen lages, før den bufres:
    /// listen sorteres på stedet, så hashen for en kopi blir billig senere.
    /// </summary>
    public void SaveInBackground(int[]? locationIds)
    {
        if (!Enabled || locationIds == null || locationIds.Length < MinStoredLength) return;

        Array.Sort(locationIds);
        var sorted = SortedDistinct(locationIds);
        var hash = Hash(sorted);
        var key = Convert.ToHexString(hash);
        Keys.AddOrUpdate(locationIds, key);

        var claim = new StoredList(DateTime.UtcNow + LocationIntersection.LocationIdsCacheDuration);
        if (!TryClaim(key, claim)) return;
        RemoveExpired();

        // Egen tilkobling, fordi forespørselens kontekst er borte før lagringen er ferdig.
        var connectionString = ((DbContext)context).Database.GetConnectionString()!;
        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(SaveTimeout);
                var listId = await SaveAsync(connectionString, hash, sorted, maxStoredRows, timeout.Token);
                if (listId == 0)
                {
                    // Ubrukte lister kan ha gjort plass. Ryddingen går ellers høyst én gang i timen, og
                    // listen ville gått som JSON så lenge den er bufret.
                    await CleanupIfDueAsync(connectionString, now: true);
                    using var retryTimeout = new CancellationTokenSource(SaveTimeout);
                    listId = await SaveAsync(connectionString, hash, sorted, maxStoredRows, retryTimeout.Token);
                }
                if (listId == 0)
                    logger.LogWarning(
                        "Lokasjonslistene har nådd taket på {Tak} rader. Listen med {Antall} lokasjoner sendes som JSON.",
                        maxStoredRows, sorted.Length);
                Volatile.Write(ref claim.ListId, listId);
            }
            catch (Exception e)
            {
                // Listen går som JSON så lenge den er bufret, og lagres på nytt neste gang den lages.
                logger.LogWarning(e, "Lagring av lokasjonsliste med {Antall} lokasjoner feilet. Søkene sender den som JSON.",
                    sorted.Length);
                Lists.TryRemove(new KeyValuePair<string, StoredList>(key, claim));
            }
            finally
            {
                claim.Done.TrySetResult();
            }

            // Etter lagringen, så søkene som venter på listen ikke venter på ryddingen også.
            await CleanupIfDueAsync(connectionString);
        });
    }

    // En gyldig oppføring betyr at listen er lagret eller lagres nå, og at raden ble merket for
    // under en time siden. Har den under halve levetiden igjen, erstattes den, så raden merkes på
    // nytt og listen lever en time til i minnet. Ellers kunne en liste som lages på nytt like før
    // oppføringen går ut, gått som JSON i nesten en time. Listens ID følger med, så søkene bruker
    // tabellen mens raden merkes.
    private static bool TryClaim(string key, StoredList claim)
    {
        while (true)
        {
            if (Lists.TryAdd(key, claim)) return true;
            if (!Lists.TryGetValue(key, out var existing)) continue;
            if (existing.ExpiresAt > DateTime.UtcNow + RenewalWindow) return false;
            Volatile.Write(ref claim.ListId, Volatile.Read(ref existing.ListId));
            if (Lists.TryUpdate(key, claim, existing)) return true;
        }
    }

    private static readonly TimeSpan RenewalWindow = LocationIntersection.LocationIdsCacheDuration / 2;

    private static void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in Lists)
            if (entry.Value.ExpiresAt <= now) Lists.TryRemove(entry);
    }

    /// <summary>0 hvis listen ikke ble lagret fordi tabellen er over taket.</summary>
    private static async Task<int> SaveAsync(
        string connectionString, byte[] hash, int[] sorted, long maxStoredRows, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (await TouchAsync(connection, hash, cancellationToken) is { } existing) return existing;

        // Taket gjelder nye lister: en som alt er lagret, merkes over også når tabellen er full.
        // To samtidige lagringer kan begge slippe gjennom, så taket kan overskrides litt.
        if (await StoredRowsAsync(connection, cancellationToken) + sorted.Length > maxStoredRows) return 0;

        return await InsertAsync(connection, hash, sorted, cancellationToken)
               ?? await TouchAsync(connection, hash, cancellationToken)
               ?? throw new InvalidOperationException("The location list was neither stored nor found.");
    }

    private static async Task<long> StoredRowsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ISNULL(SUM(CAST(LocationCount AS bigint)), 0) FROM dbo.LocationLists";
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // Finner og merker listen i én setning. Ryddingen sletter bare lister som fortsatt er
    // ubrukte, så en liste som merkes her, blir ikke slettet etterpå.
    private static async Task<int?> TouchAsync(SqlConnection connection, byte[] hash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.LocationLists SET LastUsedAt = SYSUTCDATETIME() OUTPUT INSERTED.Id WHERE Hash = @hash";
        command.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash;
        return await command.ExecuteScalarAsync(cancellationToken) as int?;
    }

    /// <summary>Null hvis en annen instans lagret samme liste samtidig.</summary>
    private static async Task<int?> InsertAsync(
        SqlConnection connection, byte[] hash, int[] sorted, CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int listId;
        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO dbo.LocationLists (Hash, LocationCount, CreatedAt, LastUsedAt)
                OUTPUT INSERTED.Id
                VALUES (@hash, @count, SYSUTCDATETIME(), SYSUTCDATETIME())
                """;
            insert.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash;
            insert.Parameters.Add("@count", SqlDbType.Int).Value = sorted.Length;
            listId = (int)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            return null;
        }

        var rows = new DataTable();
        rows.Columns.Add("ListId", typeof(int));
        rows.Columns.Add("LocationId", typeof(int));
        foreach (var locationId in sorted) rows.Rows.Add(listId, locationId);

        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = "dbo.LocationListEntries",
            BulkCopyTimeout = 300,
        };
        bulk.ColumnMappings.Add("ListId", "ListId");
        bulk.ColumnMappings.Add("LocationId", "LocationId");
        bulk.ColumnOrderHints.Add("ListId", SortOrder.Ascending);
        bulk.ColumnOrderHints.Add("LocationId", SortOrder.Ascending);
        await bulk.WriteToServerAsync(rows, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return listId;
    }

    // Høyst én gang i timen per instans, etter at en liste er lagret. Listeraden slettes først og
    // bare hvis den fortsatt er ubrukt, så ingen kan finne en liste som er halvveis slettet.
    //
    // Uten tidsgrense: et etterslep kan ta minutter (19 millioner rader lokalt), og avbrytes
    // ryddingen etter at listeradene er slettet, blir radene deres liggende igjen.
    // Med now ryddes det uansett, og neste planlagte rydding flyttes en time fram.
    private async Task CleanupIfDueAsync(string connectionString, bool now = false)
    {
        var time = DateTime.UtcNow;
        var due = Interlocked.Read(ref _nextCleanupTicks);
        if (now) Interlocked.Exchange(ref _nextCleanupTicks, (time + CleanupInterval).Ticks);
        else if (time.Ticks < due || Interlocked.CompareExchange(ref _nextCleanupTicks, (time + CleanupInterval).Ticks, due) != due) return;

        // Én rydding om gangen. Ved taket kan flere lagringer be om en samtidig, og de ville slettet
        // de samme radene og risikert vranglås. Kommer en mens en annen pågår, venter den på den.
        if (!await CleanupGate.WaitAsync(0))
        {
            await CleanupGate.WaitAsync();
            CleanupGate.Release();
            return;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var includeLeftovers = Volatile.Read(ref _leftoversRemoved) == 0;
            await DeleteUnusedAsync(connection, time - UnusedLifetime, includeLeftovers);
            if (includeLeftovers) Volatile.Write(ref _leftoversRemoved, 1);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Rydding av ubrukte lokasjonslister feilet. Prøves igjen om {Intervall}.", CleanupInterval);
        }
        finally
        {
            CleanupGate.Release();
        }
    }

    private static readonly SemaphoreSlim CleanupGate = new(1, 1);

    // Med includeLeftovers også radene etter lister som er borte, fra en rydding som ble avbrutt.
    // Det krever en skanning av alle radene, så bare én gang per oppstart: en avbrutt rydding
    // følger nesten alltid av at instansen stoppet.
    private static async Task DeleteUnusedAsync(SqlConnection connection, DateTime cutoff, bool includeLeftovers)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @deleted TABLE (Id int PRIMARY KEY);
            DELETE FROM dbo.LocationLists OUTPUT DELETED.Id INTO @deleted WHERE LastUsedAt < @cutoff;
            IF @includeLeftovers = 1
                INSERT INTO @deleted (Id)
                SELECT DISTINCT e.ListId FROM dbo.LocationListEntries e
                WHERE NOT EXISTS (SELECT 1 FROM dbo.LocationLists l WHERE l.Id = e.ListId)
                  AND NOT EXISTS (SELECT 1 FROM @deleted d WHERE d.Id = e.ListId);
            -- Under terskelen på 5 000 låser, der SQL Server trapper opp til en tabellås. Med
            -- en tabellås ville lagringene, og søkene uten radversjonering, ventet på ryddingen.
            WHILE 1 = 1
            BEGIN
                DELETE TOP (4000) e FROM dbo.LocationListEntries e JOIN @deleted d ON d.Id = e.ListId;
                IF @@ROWCOUNT = 0 BREAK;
            END
            """;
        command.Parameters.Add("@cutoff", SqlDbType.DateTime2).Value = cutoff;
        command.Parameters.Add("@includeLeftovers", SqlDbType.Bit).Value = includeLeftovers;
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync();
    }
}

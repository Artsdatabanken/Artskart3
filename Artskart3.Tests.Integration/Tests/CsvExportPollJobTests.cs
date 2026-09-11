extern alias workers;

using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using workers::Artskart3.Workers.Configuration;
using workers::Artskart3.Workers.Export;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Gjenopprettingen av hengende eksportjobber.
///
/// En jobb som DREPER prosessen (OOM, uventet exit) rekker aldri å bli merket
/// Failed av feilhåndteringen — den blir bare stående i Processing. Uten en
/// forsøksteller satte gjenopprettingen den tilbake til Pending med opprinnelig
/// CreatedAt i behold, og siden claimen plukker eldste ventende jobb var den
/// straks først i køen igjen. Resultatet var en evighetsløkke som samtidig
/// sultet ut alle andres eksporter.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class CsvExportPollJobTests : IAsyncLifetime
{
    private readonly DatabaseFixture _db;

    public CsvExportPollJobTests(DatabaseFixture db) => _db = db;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Eksportjobbene ryddes bort slik at testene ikke ser hverandres rader.
        await using var context = CreateContext();
        await context.Set<CsvExportJob>().ExecuteDeleteAsync();
    }

    [Fact]
    public async Task StuckJob_IsReturnedToPending_AndCountsAnAttempt()
    {
        var jobId = await CreateStuckJobAsync(attempts: 0);

        await RunRecoveryAsync(maxAttempts: 3);

        var job = await LoadAsync(jobId);
        job.Status.Should().Be(CsvExportStatus.Pending);
        job.Attempts.Should().Be(1);
        job.StartedAt.Should().BeNull();
        job.RowsProcessed.Should().Be(0);
    }

    /// <summary>
    /// Selve poenget: på siste forsøk skal jobben gis opp, ikke settes tilbake.
    /// </summary>
    [Fact]
    public async Task StuckJob_OnLastAttempt_IsFailedInsteadOfRetried()
    {
        var jobId = await CreateStuckJobAsync(attempts: 2);

        await RunRecoveryAsync(maxAttempts: 3);

        var job = await LoadAsync(jobId);
        job.Status.Should().Be(CsvExportStatus.Failed);
        job.Attempts.Should().Be(3);
        job.CompletedAt.Should().NotBeNull();
        job.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// En giftig jobb skal ikke kunne blokkere køen i det uendelige. Etter
    /// MaxAttempts runder må den være ute av Pending, slik at neste jobb slipper
    /// til.
    /// </summary>
    [Fact]
    public async Task PoisonJob_StopsBlockingTheQueue_AfterMaxAttempts()
    {
        var jobId = await CreateStuckJobAsync(attempts: 0);
        const int maxAttempts = 3;

        for (var i = 0; i < maxAttempts; i++)
        {
            // Hver runde simulerer at workeren døde: jobben står igjen i
            // Processing med et gammelt StartedAt.
            await MarkAsStuckAsync(jobId);
            await RunRecoveryAsync(maxAttempts);
        }

        var job = await LoadAsync(jobId);
        job.Status.Should().Be(CsvExportStatus.Failed);

        var pendingCount = await CountAsync(CsvExportStatus.Pending);
        pendingCount.Should().Be(0, "en gitt-opp jobb skal ikke ligge igjen og blokkere køen");
    }

    /// <summary>
    /// Jobber som fortsatt er innenfor tidsgrensen skal ikke røres — ellers ville
    /// gjenopprettingen avbrutt eksporter som bare tar lang tid.
    /// </summary>
    [Fact]
    public async Task RunningJob_WithinTimeout_IsLeftAlone()
    {
        var jobId = await CreateJobAsync(job =>
        {
            job.Status = CsvExportStatus.Processing;
            job.StartedAt = DateTime.UtcNow;
        });

        await RunRecoveryAsync(maxAttempts: 3);

        var job = await LoadAsync(jobId);
        job.Status.Should().Be(CsvExportStatus.Processing);
        job.Attempts.Should().Be(0);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Kjører kun gjenopprettingssteget. ExecuteAsync ville i tillegg claimet og
    /// prosessert jobben i samme kall, og da er det ikke lenger gjenopprettingen
    /// man observerer.
    /// </summary>
    private async Task RunRecoveryAsync(int maxAttempts)
    {
        var services = new ServiceCollection();
        services.AddScoped<IArtsKartDbContext>(_ => CreateContext());

        var options = Options.Create(new CsvExportOptions
        {
            Worker = new ExportWorkerOptions
            {
                StuckJobTimeoutMinutes = 10,
                MaxAttempts = maxAttempts
            }
        });

        var provider = services.BuildServiceProvider();
        var pollJob = new CsvExportPollJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CsvExportPollJob>.Instance,
            options);

        await using var context = CreateContext();
        await pollJob.RecoverStuckJobsAsync(context, CancellationToken.None);
    }

    private async Task<int> CreateStuckJobAsync(int attempts) =>
        await CreateJobAsync(job =>
        {
            job.Status = CsvExportStatus.Processing;
            job.StartedAt = DateTime.UtcNow.AddHours(-1);
            job.Attempts = attempts;
        });

    private async Task<int> CreateJobAsync(Action<CsvExportJob> configure)
    {
        await using var context = CreateContext();

        var job = new CsvExportJob
        {
            UserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa"),
            Name = "test",
            Status = CsvExportStatus.Pending,
            FilterJson = JsonSerializer.Serialize(new ObservationSearchFilterDto()),
            SelectedColumns = JsonSerializer.Serialize(new List<string>())
        };

        configure(job);

        context.Set<CsvExportJob>().Add(job);
        await context.SaveChangesAsync();

        return job.Id;
    }

    private async Task MarkAsStuckAsync(int jobId)
    {
        await using var context = CreateContext();
        await context.Set<CsvExportJob>()
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, CsvExportStatus.Processing)
                .SetProperty(j => j.StartedAt, DateTime.UtcNow.AddHours(-1)));
    }

    private async Task<CsvExportJob> LoadAsync(int jobId)
    {
        await using var context = CreateContext();
        return await context.Set<CsvExportJob>().AsNoTracking().FirstAsync(j => j.Id == jobId);
    }

    private async Task<int> CountAsync(CsvExportStatus status)
    {
        await using var context = CreateContext();
        return await context.Set<CsvExportJob>().CountAsync(j => j.Status == status);
    }

    private ArtskartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ArtskartDbContext(options);
    }
}

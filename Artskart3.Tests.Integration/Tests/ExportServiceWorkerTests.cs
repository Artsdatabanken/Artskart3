extern alias workers;

using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using workers::Artskart3.Workers.Configuration;
using workers::Artskart3.Workers.Export;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniExcelLibs;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Tester selve eksportkjøringen i workeren (<see cref="ExportService.ProcessJobAsync"/>),
/// ikke API-endepunktene. Blob storage byttes ut med en fake som holder innholdet i
/// minnet, slik at testene ikke trenger Azurite.
/// </summary>
[Collection(nameof(DatabaseCollection))]
public class ExportServiceWorkerTests
{
    private readonly DatabaseFixture _db;

    public ExportServiceWorkerTests(DatabaseFixture db) => _db = db;

    private ArtskartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(_db.ConnectionString, x => x.UseNetTopologySuite())
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ArtskartDbContext(options);
    }

    private static ExportService CreateService(ArtskartDbContext context, FakeBlobStorage blobStorage)
    {
        var options = Options.Create(new CsvExportOptions
        {
            Worker = new ExportWorkerOptions
            {
                // Testdatabasen har i underkant av 200 observasjoner. Batchen holdes
                // liten slik at eksporten faktisk går flere runder gjennom løkken og
                // dermed dekker keyset-pagineringen og fremdriftsoppdateringene.
                BatchSize = 50,
                InterBatchDelayMs = 0,
                ExpiresAtDays = 180
            }
        });

        return new ExportService(
            context,
            blobStorage,
            new CsvWriterService(),
            new ExportColumnRegistry(),
            options,
            NullLogger<ExportService>.Instance);
    }

    private static async Task<CsvExportJob> CreateJobAsync(ArtskartDbContext context, string name)
    {
        var job = new CsvExportJob
        {
            UserId = "00000000-0000-0000-0000-0000000000ff",
            Name = name,
            Status = CsvExportStatus.Processing,
            StartedAt = DateTime.UtcNow,
            FilterJson = JsonSerializer.Serialize(new ObservationSearchFilterDto()),
            SelectedColumns = JsonSerializer.Serialize(new List<string>())
        };

        context.Set<CsvExportJob>().Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task ProcessJob_WritesCsvAndExcelWithSameRowCount()
    {
        await using var context = CreateContext();
        var blobStorage = new FakeBlobStorage();
        var service = CreateService(context, blobStorage);

        var expectedRows = await context.Set<Observation>().CountAsync();
        expectedRows.Should().BeGreaterThan(0, "testdatabasen må ha observasjoner for at testen skal si noe");

        var job = await CreateJobAsync(context, "test-eksport");

        await service.ProcessJobAsync(job, CancellationToken.None);

        var saved = await context.Set<CsvExportJob>().AsNoTracking().FirstAsync(j => j.Id == job.Id);

        saved.Status.Should().Be(CsvExportStatus.Complete);
        saved.BlobPath.Should().NotBeNullOrEmpty();
        saved.ExcelBlobPath.Should().NotBeNullOrEmpty();
        saved.RowsProcessed.Should().Be(expectedRows);
        saved.TotalRows.Should().Be(expectedRows);

        // CSV: header + én linje per observasjon
        var csvBytes = blobStorage.Blobs[saved.BlobPath!];
        var csvText = System.Text.Encoding.UTF8.GetString(csvBytes);
        var csvLines = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        csvLines.Should().HaveCount(expectedRows + 1);

        // FileSize skal være antall bytes som faktisk ble skrevet
        saved.FileSize.Should().Be(csvBytes.Length);

        // Excel: header håndteres av MiniExcel, så Query gir én rad per observasjon
        using var excelStream = new MemoryStream(blobStorage.Blobs[saved.ExcelBlobPath!]);
        var excelRows = MiniExcel.Query(excelStream, useHeaderRow: true).Cast<object>().Count();

        excelRows.Should().Be(expectedRows);
    }

    [Fact]
    public async Task ProcessJob_WhenCancelledByUser_LeavesNoBlobsAndDoesNotComplete()
    {
        await using var context = CreateContext();
        var blobStorage = new FakeBlobStorage();
        var service = CreateService(context, blobStorage);

        var job = await CreateJobAsync(context, "avbrutt-eksport");

        // Brukeren kansellerer før workeren rekker første batch
        await context.Set<CsvExportJob>()
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, CsvExportStatus.Cancelled));

        await service.ProcessJobAsync(job, CancellationToken.None);

        var saved = await context.Set<CsvExportJob>().AsNoTracking().FirstAsync(j => j.Id == job.Id);

        saved.Status.Should().Be(CsvExportStatus.Cancelled);
        saved.BlobPath.Should().BeNull();
        saved.ExcelBlobPath.Should().BeNull();

        // Den halvskrevne CSV-en skal være ryddet bort
        blobStorage.Blobs.Should().BeEmpty();
    }

    /// <summary>
    /// Fake blob storage som holder blobene i minnet. Skrivestrømmen fra
    /// <see cref="OpenWriteAsync"/> committer innholdet når den lukkes, slik den
    /// ekte implementasjonen gjør.
    /// </summary>
    private sealed class FakeBlobStorage : IBlobStorageService
    {
        public Dictionary<string, byte[]> Blobs { get; } = [];

        public Task CheckConnectionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            if (content.CanSeek)
                content.Position = 0;
            content.CopyTo(ms);
            Blobs[blobPath] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream> OpenWriteAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new CommitOnCloseStream(this, blobPath));

        public Task<Stream> OpenReadStreamAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(Blobs[blobPath]));

        public Task<string> GenerateSasUrlAsync(string blobPath, TimeSpan validFor)
            => Task.FromResult($"https://fake.blob/{blobPath}");

        public Task DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default)
        {
            Blobs.Remove(blobPath);
            return Task.CompletedTask;
        }

        private sealed class CommitOnCloseStream(FakeBlobStorage owner, string blobPath) : MemoryStream
        {
            private bool _committed;

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_committed)
                {
                    _committed = true;
                    owner.Blobs[blobPath] = ToArray();
                }

                base.Dispose(disposing);
            }
        }
    }
}

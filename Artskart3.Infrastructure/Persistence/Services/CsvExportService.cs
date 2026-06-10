using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.QueryBuilders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// API-side implementasjon av ICsvExportService.
/// Håndterer opprettelse og administrasjon av eksportjobber i databasen.
/// Selve CSV-genereringen gjøres av workeren.
/// </summary>
public class CsvExportService : ICsvExportService
{
    private readonly IArtsKartDbContext _context;
    private readonly IConfiguration _configuration;

    public CsvExportService(IArtsKartDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public Task<List<ExportColumnDefinition>> GetAvailableColumnsAsync()
    {
        var columns = GetColumnDefinitions();
        return Task.FromResult(columns);
    }

    public async Task<ExportSummaryDto> GetExportSummaryAsync(ObservationSearchFilterDto filter, List<string> columns)
    {
        var query = BuildFilteredQuery(filter);
        var count = await query.CountAsync();

        var softLimit = _configuration.GetValue("CsvExport:Limits:SoftRowLimit", 500_000);
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 1_000_000);

        // Estimert filstørrelse: ~200 bytes per rad * antall valgte kolonner / totalt mulige kolonner
        var columnRatio = Math.Max(columns.Count, 1) / 50.0;
        var estimatedSize = (long)(count * 200 * columnRatio);

        return new ExportSummaryDto
        {
            TotalRows = count,
            EstimatedFileSizeBytes = estimatedSize,
            ExceedsSoftLimit = count > softLimit,
            ExceedsHardLimit = count > hardLimit
        };
    }

    public async Task<int> StartExportAsync(string userId, ObservationSearchFilterDto filter, List<string> columns)
    {
        var hardLimit = _configuration.GetValue("CsvExport:Limits:HardRowLimit", 1_000_000);
        var maxConcurrent = _configuration.GetValue("CsvExport:Limits:MaxConcurrentPerUser", 3);

        // Sjekk antall samtidige jobber for bruker
        var activeJobCount = await _context.Set<CsvExportJob>()
            .CountAsync(j => j.UserId == userId &&
                (j.Status == CsvExportStatus.Pending || j.Status == CsvExportStatus.Processing));

        if (activeJobCount >= maxConcurrent)
            throw new InvalidOperationException(
                $"Maks {maxConcurrent} samtidige eksportjobber per bruker.");

        // Sjekk hard limit
        var query = BuildFilteredQuery(filter);
        var count = await query.CountAsync();

        if (count > hardLimit)
            throw new InvalidOperationException(
                $"Antall rader ({count}) overstiger grensen ({hardLimit}).");

        var job = new CsvExportJob
        {
            UserId = userId,
            Status = CsvExportStatus.Pending,
            FilterJson = JsonSerializer.Serialize(filter),
            SelectedColumns = JsonSerializer.Serialize(columns),
            TotalRows = count
        };

        _context.Set<CsvExportJob>().Add(job);
        await _context.SaveChangesAsync();

        return job.Id;
    }

    public async Task<CsvExportJobDto?> GetJobStatusAsync(int jobId)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId);

        return job == null ? null : MapToDto(job);
    }

    public async Task<bool> CancelExportAsync(int jobId, string userId)
    {
        var job = await _context.Set<CsvExportJob>()
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

        if (job == null)
            return false;

        if (job.Status != CsvExportStatus.Pending && job.Status != CsvExportStatus.Processing)
            return false;

        job.Status = CsvExportStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<string?> GetDownloadUrlAsync(int jobId)
    {
        var job = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job?.Status != CsvExportStatus.Complete || string.IsNullOrEmpty(job.BlobPath))
            return null;

        return job.BlobPath;
    }

    public async Task<List<CsvExportJobDto>> GetUserExportHistoryAsync(string userId)
    {
        var jobs = await _context.Set<CsvExportJob>()
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync();

        return jobs.Select(MapToDto).ToList();
    }

    private IQueryable<Observation> BuildFilteredQuery(ObservationSearchFilterDto filter)
    {
        var query = _context.Set<Observation>().AsNoTracking();
        return ObservationQueryBuilder.ApplyFilters(_context, query, filter);
    }

    private static CsvExportJobDto MapToDto(CsvExportJob job) => new()
    {
        Id = job.Id,
        Status = job.Status,
        TotalRows = job.TotalRows,
        RowsProcessed = job.RowsProcessed,
        FileSize = job.FileSize,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        ExpiresAt = job.ExpiresAt,
        ErrorMessage = job.ErrorMessage
    };

    private static List<ExportColumnDefinition> GetColumnDefinitions() =>
    [
        new() { Name = "Id", DisplayName = "Id", IsDefaultSelected = true },
        new() { Name = "OccurrenceId", DisplayName = "OccurrenceId", IsDefaultSelected = true },
        new() { Name = "InstitutionCode", DisplayName = "Institusjonskode", IsDefaultSelected = true },
        new() { Name = "CollectionCode", DisplayName = "Samlingskode", IsDefaultSelected = true },
        new() { Name = "CatalogNumber", DisplayName = "Katalognummer", IsDefaultSelected = true },
        new() { Name = "DateTimeCollected", DisplayName = "Innsamlingsdato", IsDefaultSelected = true },
        new() { Name = "TaxonId", DisplayName = "Takson-ID", IsDefaultSelected = true },
        new() { Name = "Latitude", DisplayName = "Breddegrad", IsDefaultSelected = true },
        new() { Name = "Longitude", DisplayName = "Lengdegrad", IsDefaultSelected = true },
        new() { Name = "CoordinatePrecisionInMeters", DisplayName = "Koordinatpresisjon (meter)", IsDefaultSelected = true },
        new() { Name = "Detail.DatasetName", DisplayName = "Datasett", IsDefaultSelected = true },
        new() { Name = "Detail.Collector", DisplayName = "Innsamler", IsDefaultSelected = true },
        new() { Name = "Detail.Locality", DisplayName = "Lokalitet", IsDefaultSelected = true },
        new() { Name = "ProxyId", DisplayName = "ProxyId" },
        new() { Name = "NodeId", DisplayName = "NodeId" },
        new() { Name = "BasisOfRecordId", DisplayName = "Funntype-ID" },
        new() { Name = "DatetimeIdentified", DisplayName = "Bestemmelsesdato" },
        new() { Name = "DateLastModified", DisplayName = "Sist endret" },
        new() { Name = "DateTimeRecordImported", DisplayName = "Importert" },
        new() { Name = "MatchedScientificNameId", DisplayName = "Matchet vitenskapelig navn-ID" },
        new() { Name = "TaxonGroupId", DisplayName = "Taksongruppe-ID" },
        new() { Name = "CategoryId", DisplayName = "Kategori-ID" },
        new() { Name = "East", DisplayName = "Øst (UTM33)" },
        new() { Name = "North", DisplayName = "Nord (UTM33)" },
        new() { Name = "YearCollected", DisplayName = "Innsamlingsår" },
        new() { Name = "MonthCollected", DisplayName = "Innsamlingsmåned" },
        new() { Name = "HasErrors", DisplayName = "Har feil" },
        new() { Name = "HasAnnotations", DisplayName = "Har annoteringer" },
        new() { Name = "ObservationQualityTypeId", DisplayName = "Kvalitetstype-ID" },
        new() { Name = "Detail.DatasetId", DisplayName = "Datasett-ID" },
        new() { Name = "Detail.IndividualCount", DisplayName = "Antall individer" },
        new() { Name = "Detail.Notes", DisplayName = "Merknader" },
        new() { Name = "Detail.IdentifiedBy", DisplayName = "Bestemt av" },
        new() { Name = "Detail.Habitat", DisplayName = "Habitat" },
        new() { Name = "Detail.Sex", DisplayName = "Kjønn" },
        new() { Name = "Detail.CollectingMethod", DisplayName = "Innsamlingsmetode" },
        new() { Name = "Detail.RecordNumber", DisplayName = "Postnummer" },
        new() { Name = "Detail.FieldNumber", DisplayName = "Feltnummer" },
        new() { Name = "Detail.MeasurementMethod", DisplayName = "Målemetode" },
        new() { Name = "Detail.GeoreferenceRemarks", DisplayName = "Georeferanse-merknader" },
        new() { Name = "Detail.Preparations", DisplayName = "Preparering" },
        new() { Name = "Detail.OtherCatalogNumbers", DisplayName = "Andre katalognumre" },
        new() { Name = "Detail.TypeStatus", DisplayName = "Typestatus" },
        new() { Name = "Detail.EventTime", DisplayName = "Hendelsestid" },
        new() { Name = "Detail.MaximumElevationInMeters", DisplayName = "Maks høyde (meter)" },
        new() { Name = "Detail.MinimumElevationInMeters", DisplayName = "Min høyde (meter)" },
        new() { Name = "Detail.VerbatimDepth", DisplayName = "Dybde (tekst)" },
        new() { Name = "Detail.DynamicProperties", DisplayName = "Dynamiske egenskaper" },
        new() { Name = "Detail.AssociatedReferences", DisplayName = "Tilknyttede referanser" },
    ];
}

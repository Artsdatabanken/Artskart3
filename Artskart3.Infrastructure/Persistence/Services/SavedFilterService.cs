using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Services;

public class SavedFilterService : ISavedFilterService
{
    private readonly IArtsKartDbContext _context;

    public SavedFilterService(IArtsKartDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedFilterDto>> GetUserFiltersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var filters = await _context.Set<SavedFilter>()
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return filters.Select(MapToDto).ToList();
    }

    public async Task<SavedFilterDto> CreateAsync(Guid userId, CreateSavedFilterRequestDto request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        filter.PageNumber = null;
        filter.ResultsPerPage = null;

        var savedFilter = new SavedFilter
        {
            UserId = userId,
            Name = request.Name.Trim(),
            FilterJson = JsonSerializer.Serialize(filter),
            MinX = request.Extent?.MinX,
            MinY = request.Extent?.MinY,
            MaxX = request.Extent?.MaxX,
            MaxY = request.Extent?.MaxY,
            IsDefault = request.IsDefault,
        };

        if (request.IsDefault)
        {
            await ClearDefaultsAsync(userId, cancellationToken);
        }

        _context.Set<SavedFilter>().Add(savedFilter);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(savedFilter);
    }

    public async Task<bool> DeleteAsync(Guid publicId, Guid userId, CancellationToken cancellationToken)
    {
        var savedFilter = await _context.Set<SavedFilter>()
            .FirstOrDefaultAsync(f => f.PublicId == publicId && f.UserId == userId, cancellationToken);

        if (savedFilter == null)
            return false;

        savedFilter.IsDeleted = true;
        savedFilter.DeletedAt = DateTime.UtcNow;
        savedFilter.IsDefault = false;
        savedFilter.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> SetDefaultAsync(Guid publicId, Guid userId, bool isDefault, CancellationToken cancellationToken)
    {
        var savedFilter = await _context.Set<SavedFilter>()
            .FirstOrDefaultAsync(f => f.PublicId == publicId && f.UserId == userId, cancellationToken);

        if (savedFilter == null)
            return false;

        if (isDefault)
        {
            await ClearDefaultsAsync(userId, cancellationToken);
        }

        savedFilter.IsDefault = isDefault;
        savedFilter.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    // Endringene lagres sammen med kallerens SaveChangesAsync, så byttet av
    // standardfilter skjer i én transaksjon.
    private async Task ClearDefaultsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _context.Set<SavedFilter>()
            .Where(f => f.UserId == userId && f.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var current in currentDefaults)
        {
            current.IsDefault = false;
            current.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static SavedFilterDto MapToDto(SavedFilter savedFilter) => new()
    {
        Id = savedFilter.PublicId,
        Name = savedFilter.Name,
        Filter = JsonSerializer.Deserialize<ObservationSearchFilterDto>(savedFilter.FilterJson) ?? new ObservationSearchFilterDto(),
        Extent = savedFilter is { MinX: { } minX, MinY: { } minY, MaxX: { } maxX, MaxY: { } maxY }
            ? new MapExtentDto { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY }
            : null,
        IsDefault = savedFilter.IsDefault,
        // SQL Server returnerer DateTimeKind.Unspecified; uten Utc tolker nettleseren tiden som lokal.
        CreatedAt = DateTime.SpecifyKind(savedFilter.CreatedAt, DateTimeKind.Utc),
    };
}

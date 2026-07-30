using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly IArtsKartDbContext _context;
    private readonly ILogger<LookupRepository> _logger;

    public LookupRepository(IArtsKartDbContext context, ILogger<LookupRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<CategoryType>()
                .Where(ct => !ct.IsDeleted)
                .OrderBy(ct => ct.Name)
                .Select(ct => new CategoryTypeDto
                {
                    Id = ct.Id,
                    Name = ct.Name,
                    Categories = ct.Categories
                        .Where(c => !c.IsDeleted)
                        .OrderBy(c => c.Name)
                        .Select(c => new CategoryDto
                        {
                            Id = c.Id,
                            Code = c.Code,
                            Name = c.Name,
                            ObservationCount = c.ObservationCount
                        })
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av kategorier");
            throw new ApplicationException("Feil ved henting av kategorier", ex);
        }
    }

    public async Task<IEnumerable<AreaTypeDto>> GetAreasAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<AreaType>()
                .Where(at => !at.IsDeleted)
                .OrderBy(at => at.Name)
                .Select(at => new AreaTypeDto
                {
                    Id = at.Id,
                    Name = at.Name,
                    Areas = at.Areas
                        .Where(a => !a.IsDeleted && a.IsCurrent)
                        .OrderBy(a => a.Name)
                        .Select(a => new AreaDto
                        {
                            Id = a.Id,
                            Fid = a.Fid,
                            Name = a.Name,
                            IsCurrent = a.IsCurrent,
                            ObservationCount = a.ObservationCount
                        })
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av områder");
            throw new ApplicationException("Feil ved henting av områder", ex);
        }
    }

    private const int InstitutionOrganizationTypeId = 1;

    public async Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<Organization>()
                .Where(o => !o.IsDeleted && o.OrganizationTypeId == InstitutionOrganizationTypeId)
                .OrderBy(o => o.Name)
                .Select(o => new InstitutionDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    Code = o.Code,
                    ObservationCount = o.ObservationCount
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av institusjoner");
            throw new ApplicationException("Feil ved henting av institusjoner", ex);
        }
    }

    public async Task<IEnumerable<OrganizationDto>> SearchOrganizationsAsync(string name, int maxCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Enumerable.Empty<OrganizationDto>();
            }

            var searchPattern = "%" + name.Trim().EscapeSqlLikePattern() + "%";

            return await _context.Set<Organization>()
                .Where(o => !o.IsDeleted && EF.Functions.Like(o.Name, searchPattern))
                .OrderBy(o => o.Name)
                .Take(maxCount)
                .Select(o => new OrganizationDto
                {
                    Id = o.Id,
                    Name = o.Name
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved søk etter organisasjoner med navn: {Name}", name);
            throw new ApplicationException("Feil ved søk etter organisasjoner", ex);
        }
    }

    public async Task<IEnumerable<TaxonGroupDto>> GetTaxonGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<TaxonGroup>()
                .Where(tg => !tg.IsDeleted)
                .OrderBy(tg => tg.Name)
                .Select(tg => new TaxonGroupDto
                {
                    Id = tg.Id,
                    Name = tg.Name,
                    ObservationCount = tg.ObservationCount
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av taksongrupper");
            throw new ApplicationException("Feil ved henting av taksongrupper", ex);
        }
    }

    public async Task<IEnumerable<BehaviorDto>> GetBehaviorsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<Behavior>()
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new BehaviorDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Variants = b.Variants,
                    ObservationCount = b.ObservationCount,
                    Description = b.Description
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av atferdstyper");
            throw new ApplicationException("Feil ved henting av atferdstyper", ex);
        }
    }

    public async Task<IEnumerable<BasisOfRecordDto>> GetBasisOfRecordsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Set<BasisOfRecord>()
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.Name)
                .Select(b => new BasisOfRecordDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    Variants = b.Variants,
                    ObservationCount = b.ObservationCount
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av innsamlingsmetoder");
            throw new ApplicationException("Feil ved henting av innsamlingsmetoder", ex);
        }
    }
}

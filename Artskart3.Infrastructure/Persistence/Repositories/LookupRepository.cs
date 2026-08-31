using Artskart3.Core.Constants;
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
                .Where(o => !o.IsDeleted
                    && o.OrganizationTypeId == InstitutionOrganizationTypeId
                    && o.ObservationCount != null
                    && o.ObservationCount > 0)
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

    /// <summary>
    /// Typeahead for samling (OrganizationTypeId = 2) og prosjekt/datasett (3).
    ///
    /// Organization har 25 943 rader, så delstrengsøk er gratis her. Det er kun
    /// filterspørringen mot 61M/192M rader som ikke tåler strengsammenligning —
    /// derfor returnerer denne en ID som frontend sender videre som filter.
    /// </summary>
    public async Task<IEnumerable<OrganizationDto>> SearchOrganizationsByTypeAsync(
        string name, int organizationTypeId, int maxCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Enumerable.Empty<OrganizationDto>();
            }

            var searchPattern = "%" + name.Trim().EscapeSqlLikePattern() + "%";

            return await _context.Set<Organization>()
                .Where(o => !o.IsDeleted
                    && o.OrganizationTypeId == organizationTypeId
                    && EF.Functions.Like(o.Name, searchPattern))
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
            _logger.LogError(ex, "Feil ved søk etter organisasjoner av type {TypeId} med navn: {Name}",
                organizationTypeId, name);
            throw new ApplicationException("Feil ved søk etter organisasjoner", ex);
        }
    }

    /// <summary>
    /// Typeahead for katalognummer. Returnerer hvert treff med ObservationId-ene
    /// det peker på, slik at filteret kan sende IDer i stedet for en streng.
    ///
    /// PREFIKSSØK, IKKE DELSTRENG. 'x%' kan bruke IX_Observation_CatalogNumber som
    /// et range seek; '%x%' kan ikke indekseres og ville skannet 61M rader — som
    /// er nøyaktig det denne endringen fjernet.
    ///
    /// To spørringer med vilje: først de distinkte numrene innenfor prefikset
    /// (seek + TOP), deretter ID-ene for akkurat dem. Å bygge ID-listene i én
    /// gruppert spørring lar seg ikke oversette pålitelig av EF, og gevinsten
    /// ville uansett vært null — begge er indekserte oppslag på en håndfull rader.
    /// </summary>
    public async Task<IEnumerable<CatalogNumberMatchDto>> SearchCatalogNumbersAsync(
        string search, int maxCount, CancellationToken cancellationToken = default)
    {
        try
        {
            var trimmed = search?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return Enumerable.Empty<CatalogNumberMatchDto>();
            }

            // Starter søket med %, _ eller [, blir det escapede mønsteret et
            // klammeuttrykk ('[%]abc%'). SQL Server kan ikke bruke det som
            // seek-prefiks, og oppslaget degraderer til full skanning av
            // IX_Observation_CatalogNumber over 61M rader — på et anonymt endepunkt.
            if (trimmed[0] is '%' or '_' or '[')
            {
                return Enumerable.Empty<CatalogNumberMatchDto>();
            }

            // Serverside minstelengde. Debouncing og MinProjectNameSearchLength i
            // frontend gjelder bare nettleseren; endepunktet kan kalles direkte.
            if (trimmed.Length < SearchConstants.MinCatalogNumberSearchLength)
            {
                return Enumerable.Empty<CatalogNumberMatchDto>();
            }

            var prefixPattern = trimmed.EscapeSqlLikePattern() + "%";

            var catalogNumbers = await _context.Set<Observation>()
                .AsNoTracking()
                .Where(o => o.CatalogNumber != null && EF.Functions.Like(o.CatalogNumber, prefixPattern))
                .Select(o => o.CatalogNumber!)
                .Distinct()
                .OrderBy(n => n)
                .Take(maxCount)
                .ToListAsync(cancellationToken);

            if (catalogNumbers.Count == 0)
            {
                return Enumerable.Empty<CatalogNumberMatchDto>();
            }

            var matches = await _context.Set<Observation>()
                .AsNoTracking()
                .Where(o => o.CatalogNumber != null && catalogNumbers.Contains(o.CatalogNumber))
                .Select(o => new { o.Id, o.CatalogNumber })
                .ToListAsync(cancellationToken);

            // Grupperingen må være case-insensitiv, som databasens sortering.
            // Med ordinal sammenligning ville 'abc-1' og 'ABC-1' — som SQL Server
            // slo sammen til én rad i spørringen over — blitt splittet i to DTO-er:
            // flere treff enn maxCount, og et valg som bare får med den ene
            // skrivemåtens observasjoner. Take-en gjentas av samme grunn.
            return matches
                .GroupBy(m => m.CatalogNumber!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CatalogNumberMatchDto
                {
                    CatalogNumber = g.Key,
                    ObservationIds = g.Select(m => m.Id).ToArray()
                })
                .OrderBy(m => m.CatalogNumber, StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved søk etter katalognummer: {Search}", search);
            throw new ApplicationException("Feil ved søk etter katalognummer", ex);
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

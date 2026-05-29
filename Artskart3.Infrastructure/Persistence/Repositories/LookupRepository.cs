using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Repositories
{
    public class LookupRepository : ILookupRepository
    {
        private readonly IArtsKartDbContext _context;

        public LookupRepository(IArtsKartDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<CategoryTypeDto>> GetCategoriesAsync()
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
                            Name = c.Name
                        })
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<AreaTypeDto>> GetAreasAsync()
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
                            IsCurrent = a.IsCurrent
                        })
                })
                .ToListAsync();
        }

        private const int InstitutionOrganizationTypeId = 1;

        public async Task<IEnumerable<InstitutionDto>> GetInstitutionsAsync()
        {
            return await _context.Set<Organization>()
                .Where(o => !o.IsDeleted && o.OrganizationTypeId == InstitutionOrganizationTypeId)
                .OrderBy(o => o.Name)
                .Select(o => new InstitutionDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    Code = o.Code
                })
                .ToListAsync();
        }
    }
}

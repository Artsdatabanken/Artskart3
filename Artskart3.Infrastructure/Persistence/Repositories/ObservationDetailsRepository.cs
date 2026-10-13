using System.Text.RegularExpressions;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public sealed partial class ObservationDetailsRepository(
    IArtsKartDbContext context,
    ILogger<ObservationDetailsRepository> logger) : IObservationDetailsRepository
{
    private static readonly string[] ImageTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/avif"];

    public async Task<ObservationDetailDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        // Only public Observation/Location data; never join SensitiveObservationData.
        var observation = context.Set<Observation>().AsNoTracking().Where(o => o.Id == id && !o.IsDeleted);
        var detail = await observation.Select(o => new ObservationDetailDto
        {
            Id = o.Id,
            PopularName = o.Taxon.PreferredPopularName,
            ScientificName = o.Taxon.ValidScientificName != null
                ? o.Taxon.ValidScientificName.Replace("<i>", "").Replace("</i>", "") : null,
            // Imported legacy taxon keys are public Artsdatabanken IDs until ExternalTaxonId is populated.
            ExternalTaxonId = o.Taxon.ExternalTaxonId > 0 ? o.Taxon.ExternalTaxonId : o.TaxonId,
            CategoryCode = o.Category != null ? o.Category.Code : null,
            CategoryTypeId = o.Category != null ? o.Category.CategoryTypeId : null,
            Collected = o.DateTimeCollected,
            Collector = o.ObservationDetail != null ? o.ObservationDetail.Collector : null,
            BasisOfRecord = o.BasisOfRecord.Name,
            Behaviors = o.Behaviors.Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => b.Name).ToList(),
            Quality = o.ObservationQualityTypeId,
            Tags = o.Tags.Where(t => !t.IsDeleted).OrderBy(t => t.Id).Select(t => t.Name).ToList(),
            HasErrors = o.HasErrors,
            Locality = o.Location != null ? o.Location.Locality : null,
            Counties = o.Location != null
                ? o.Location.Areas.Where(a => !a.IsDeleted && a.IsCurrent && a.AreaTypeId == 2)
                    .OrderBy(a => a.Name).Select(a => a.Name).ToList() : new List<string>(),
            Municipalities = o.Location != null
                ? o.Location.Areas.Where(a => !a.IsDeleted && a.IsCurrent && a.AreaTypeId == 1)
                    .OrderBy(a => a.Name).Select(a => a.Name).ToList() : new List<string>(),
            Point = new ObservationPointDto(o.Latitude, o.Longitude, o.East, o.North),
            CoordinatePrecision = o.CoordinatePrecisionInMeters,
            Institution = context.Set<Organization>().Where(org => org.Id == o.InstitutionOrgId && !org.IsDeleted)
                .Select(org => org.Name).FirstOrDefault(),
            Dataset = context.Set<Organization>().Where(org => org.Id == o.DatasetOrgId && !org.IsDeleted)
                .Select(org => org.Name).FirstOrDefault(),
            CatalogNumber = o.CatalogNumber,
        }).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
        if (detail is null) return null;

        detail.Projects = await (
            from project in context.Set<ObservationProject>().AsNoTracking()
            join organization in context.Set<Organization>().AsNoTracking() on project.ProjectOrgId equals organization.Id
            where project.ObservationId == id && !organization.IsDeleted
            orderby organization.Name, organization.Id
            select organization.Name).ToListAsync(cancellationToken);

        detail.Images = await context.Set<MediaFile>().AsNoTracking()
            .Where(m => m.ObservationId == id && !m.IsDeleted && !m.MediaFileType.IsDeleted &&
                ImageTypes.Contains(m.MediaFileType.MimeType!))
            .OrderBy(m => m.Id)
            .Select(m => new ObservationMediaDto
            {
                Id = m.Id, Origin = m.Origin, MimeType = m.MediaFileType.MimeType!,
                HasStoredImage = m.Image != null && m.Image.Length > 0,
                Description = m.Description, RightsHolder = m.RightsHolder, License = m.License,
            }).ToListAsync(cancellationToken);

        if (detail.CategoryCode is not null)
        {
            var metadata = await observation.Select(o => new
            {
                o.TaxonId,
                Svalbard = o.Location != null && o.Location.Areas.Any(a => a.AreaTypeId == 2 && a.Fid == "21" && a.IsCurrent),
            }).SingleAsync(cancellationToken);
            var properties = await context.Set<TaxonProperty>().AsNoTracking()
                .Where(p => p.TaxonId == metadata.TaxonId && !p.IsDeleted && p.Tag == detail.CategoryCode &&
                    (p.Context == "N" || (metadata.Svalbard && p.Context == "S")))
                .Select(p => new { p.Context, p.Prefix, p.Url }).ToListAsync(cancellationToken);
            var regional = metadata.Svalbard && properties.Any(p => p.Context == "S")
                ? properties.Where(p => p.Context == "S") : properties.Where(p => p.Context == "N");
            var dated = regional.Select(p => new { p.Url, Year = EditionYear().Match(p.Prefix).Value })
                .Where(p => p.Year.Length > 0).ToList();
            var latest = dated.OrderByDescending(p => p.Year, StringComparer.Ordinal).FirstOrDefault()?.Year;
            var urls = dated.Where(p => p.Year == latest).Select(p => p.Url).Distinct().ToList();
            if (urls.Count == 1) detail.AssessmentUrl = urls[0];
            else if (properties.Count > 0)
                logger.LogWarning("No unambiguous assessment edition for observation {ObservationId}", id);
        }
        return detail;
    }

    public async Task<ObservationImageFile?> GetImageAsync(int observationId, int mediaId, CancellationToken cancellationToken)
    {
        var image = await context.Set<MediaFile>().AsNoTracking()
            .Where(m => m.Id == mediaId && m.ObservationId == observationId && !m.IsDeleted &&
                m.Observation != null && !m.Observation.IsDeleted && !m.MediaFileType.IsDeleted &&
                ImageTypes.Contains(m.MediaFileType.MimeType!))
            .Select(m => new { m.Image, m.MediaFileType.MimeType }).SingleOrDefaultAsync(cancellationToken);
        return image?.Image is { Length: > 0 } bytes
            ? new ObservationImageFile(bytes, image.MimeType!) : null;
    }

    [GeneratedRegex(@"(?:19|20)\d{2}")]
    private static partial Regex EditionYear();
}

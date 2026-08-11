using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Constants;
using Artskart3.Core.Domain.BusinessModels;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.RepositoryInterfaces;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TagEnum = Artskart3.Core.Domain.Enums.Tag;

namespace Artskart3.Infrastructure.Persistence.Repositories;

public class SearchRepository : ISearchRepository
{
    private const string SqlWildcard = "%";

    private readonly IArtsKartDbContext _context;
    private readonly ILogger<SearchRepository> _logger;
    private readonly PaginationOptions _paginationOptions;
    private readonly IAreaHierarchyService _areaHierarchy;

    public SearchRepository(IArtsKartDbContext context, ILogger<SearchRepository> logger, IOptions<PaginationOptions> paginationOptions, IAreaHierarchyService areaHierarchy)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _paginationOptions = paginationOptions.Value;
        _areaHierarchy = areaHierarchy ?? throw new ArgumentNullException(nameof(areaHierarchy));
    }
    /// <summary>
    /// Searches for taxa by name using a three-level matching strategy:
    /// 1. Exact matches on scientific or common names
    /// 2. Starts-with matches
    /// 3. Contains matches
    /// Returns up to maxCount results from active taxa (not deleted and have observation data).
    /// </summary>
    public async Task<IEnumerable<TaxonDto>> GetTaxonsAsync(string name, int maxCount = SearchConstants.DefaultMaxTaxonCount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Enumerable.Empty<TaxonDto>();
            }

            if (maxCount < SearchConstants.MinTaxonResults || maxCount > SearchConstants.MaxTaxonCount)
            {
                throw new ArgumentException(
                    $"Max count must be between {SearchConstants.MinTaxonResults} and {SearchConstants.MaxTaxonCount}.",
                    nameof(maxCount));
            }

            var searchTerm = name.Trim().ToLower().EscapeSqlLikePattern();

            var matchingIds = GetExactMatches(searchTerm)
                .Union(GetStartsWithMatches(searchTerm))
                .Union(GetContainsMatches(searchTerm))
                .Distinct()
                .Take(maxCount);

            var result = await _context.Set<Taxon>()
                .Where(t => matchingIds.Contains(t.Id))
                .Select(t => new TaxonDto
                {
                    Id = t.Id,
                    ExternalTaxonId = t.ExternalTaxonId,
                    ValidScientificName = t.ValidScientificName,
                    ValidScientificNameAuthorship = t.ValidScientificNameAuthorship,
                    PreferredPopularName = t.PreferredPopularName,
                    TaxonGroupId = t.TaxonGroupId,
                    TaxonRankId = t.TaxonRankId,
                    CumulativeObservationCount = t.CumulativeObservationCount,
                    ExistsInCountry = t.ExistsInCountry
                })
                .ToListAsync(cancellationToken);

            return result;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Argument validation failed for taxon search with name: {Name}", name);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error occurred during taxon search for name: {Name}", name);
            throw new ApplicationException("A database error occurred while searching taxa. Please try again later.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error during taxon search for name: {Name}", name);
            throw new ApplicationException("An unexpected error occurred while searching taxa. Please contact support if the problem persists.", ex);
        }
    }
    private IQueryable<Taxon> GetActiveTaxa()
    {
        return _context.Set<Taxon>()
            .Where(t => !t.IsDeleted && (t.CumulativeObservationCount > 0 || t.ExistsInCountry));
    }
    private IQueryable<int> GetExactMatches(string searchTerm)
    {
        return GetActiveTaxa()
            .Where(t =>
                t.TaxonNames.Any(tn => !tn.IsDeleted && EF.Functions.Like(tn.ScientificName, searchTerm)) ||
                t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted && EF.Functions.Like(tpn.Name, searchTerm))
            )
            .Select(t => t.Id);
    }
    private IQueryable<int> GetStartsWithMatches(string searchTerm)
    {
        var startsWithPattern = searchTerm + SqlWildcard;

        return GetActiveTaxa()
            .Where(t =>
                t.TaxonNames.Any(tn => !tn.IsDeleted &&
                    EF.Functions.Like(tn.ScientificName, startsWithPattern))
                ||
                t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted &&
                    EF.Functions.Like(tpn.Name, startsWithPattern))
            )
            .Select(t => t.Id);
    }
    private IQueryable<int> GetContainsMatches(string searchTerm)
    {
        var containsPattern = SqlWildcard + searchTerm + SqlWildcard;

        return GetActiveTaxa()
            .Where(t =>
                t.TaxonNames.Any(tn => !tn.IsDeleted &&
                    EF.Functions.Like(tn.ScientificName, containsPattern))
                ||
                t.TaxonPopularNames.Any(tpn => !tpn.IsDeleted &&
                    EF.Functions.Like(tpn.Name, containsPattern))
            )
            .Select(t => t.Id);
    }


    public async Task<List<ObservationDto>> GetObservationsAsync(ObservationSearchFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Observation>()
                            .AsNoTracking();

        // Observasjonsspesifikke tekstfiltre
        if (!string.IsNullOrEmpty(filter.PreferredPopularName))
        {
            var popularNamePattern = SqlWildcard + filter.PreferredPopularName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.Taxon.PreferredPopularName, popularNamePattern));
        }

        if (!string.IsNullOrEmpty(filter.ScientificName))
        {
            var scientificNamePattern = SqlWildcard + filter.ScientificName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificName, scientificNamePattern));
        }

        if (!string.IsNullOrEmpty(filter.Author))
        {
            var authorPattern = SqlWildcard + filter.Author.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificNameAuthorship, authorPattern));
        }

        // Felles filtre (taksongruppe, kategori, område, atferd, presisjon, periode)
        query = ApplyCommonFilters(query, filter);

        query = query.OrderBy(o => o.Id);

        if (filter.IsPaginated)
        {
            var skip = (filter.PageNumber!.Value - 1) * filter.ResultsPerPage!.Value;
            if (skip > 0)
            {
                query = query.Skip(skip);
            }
            query = query.Take(filter.ResultsPerPage!.Value * _paginationOptions.LookaheadMultiplier);
        }
        else
        {
            query = query.Take(SearchConstants.DefaultMaxObservations);
        }

        // Merk: Subspørringene for Institution og MunicipalityId ser ut som korrelerte N+1-spørringer,
        // men EF Core kompilerer hele LINQ-uttrykket til én enkelt SQL-setning med skalare subselects.
        // ObservationEntityIndex har PK på (ObservationId, EntityTypeId, EntityId), så hver
        // subselect er et indeksoppslag på maks 1 rad. Resultatet er allerede begrenset via Take(),
        // så dette er effektivt nok. Batch-lasting med ekstra round-trip ville vært tregere.
        return await query.Select(o => new ObservationDto
        {
            Id = o.Id,
            PreferredPopularName = o.Taxon.PreferredPopularName,
            ScientificName = o.Taxon.ValidScientificName,
            Author = o.Taxon.ValidScientificNameAuthorship,
            Institution = _context.Set<ObservationEntityIndex>()
                .Where(idx => idx.ObservationId == o.Id && idx.EntityTypeId == (int)ObservationIndexEntityType.Institution)
                .Join(_context.Set<Organization>(),
                    idx => idx.EntityId, org => org.Id,
                    (idx, org) => org.Name)
                .FirstOrDefault(),
            Locality = o.Location != null ? o.Location.Locality : null,
            MunicipalityId = _context.Set<ObservationEntityIndex>()
                .Where(idx => idx.ObservationId == o.Id && idx.EntityTypeId == (int)ObservationIndexEntityType.Municipality)
                .Select(idx => idx.EntityId.ToString())
                .FirstOrDefault(),
            TaxonGroupId = o.TaxonGroupId,
            CategoryId = o.CategoryId,
            DateTimeCollected = o.DateTimeCollected,
            CoordinatePrecisionInMeters = o.CoordinatePrecisionInMeters
        }).ToListAsync(cancellationToken);
    }



    /// <summary>
    /// Henter observasjonslokasjoner filtrert etter taksongruppe, kategori, område m.m.
    /// Aggregerer observasjonsantall per lokasjon, sortert synkende.
    /// </summary>
    public async Task<List<LocationModel>> GetLocationsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filter ??= new LocationSearchFilterDto();

            var query = _context.Set<Observation>().AsNoTracking();
            query = ApplyCommonFilters(query, filter);

            // Envelope-filter via Location-tabellen (bruker IX_EastNorth-indeksen)
            if (filter.Envelope != null)
            {
                var minX = (int)filter.Envelope.MinX;
                var maxX = (int)filter.Envelope.MaxX;
                var minY = (int)filter.Envelope.MinY;
                var maxY = (int)filter.Envelope.MaxY;
                query = query.Where(o => o.Location != null &&
                    o.Location.East >= minX && o.Location.East <= maxX &&
                    o.Location.North >= minY && o.Location.North <= maxY);
            }

            var maxResults = filter.MaxResults > 0 && filter.MaxResults <= SearchConstants.MaxLocationResults
                ? filter.MaxResults
                : SearchConstants.DefaultMaxLocations;

            // Én spørring: gruppér på lokasjon, hent koordinater og tell observasjoner
            var locationModels = await query
                .Where(o => o.Location != null)
                .GroupBy(o => new
                {
                    LocationId = o.LocationId!.Value,
                    o.Location!.Latitude,
                    o.Location!.Longitude
                })
                .Select(g => new LocationModel
                {
                    Id = g.Key.LocationId,
                    Latitude = g.Key.Latitude ?? 0,
                    Longitude = g.Key.Longitude ?? 0,
                    ObservationCount = g.Count()
                })
                .OrderByDescending(x => x.ObservationCount)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Location search completed successfully. Returned {LocationCount} locations", locationModels.Count);

            return locationModels;
        }
        catch (InvalidOperationException ex)
        {
            throw new ApplicationException("Failed to retrieve locations due to an invalid operation. Please verify your search parameters.", ex);
        }
        catch (DbUpdateException ex)
        {
            throw new ApplicationException("A database error occurred while retrieving locations. Please try again later.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ApplicationException("An unexpected error occurred while retrieving locations. Please contact support if the problem persists.", ex);
        }
    }

    /// <summary>
    /// Legger til felles filterpredikater (taksongruppe, kategori, område, atferd, etc.) på en observasjonsspørring.
    /// Brukes av både lokasjons- og områdemarkørspørringer.
    /// </summary>
    private IQueryable<Observation> ApplyCommonFilters(IQueryable<Observation> query, IObservationFilter filter)
    {
        if (filter.TaxonGroupIds?.Any() == true)
        {
            var taxonGroupIds = filter.TaxonGroupIds.ToList();
            query = query.Where(o => taxonGroupIds.Contains(o.TaxonGroupId));
        }

        if (filter.CategoryIds?.Any() == true)
        {
            var categoryIds = filter.CategoryIds.ToList();
            query = query.Where(o => o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value));
        }

        // Geografiske områdefiltre via ObservationEntityIndex (OR — observasjon i minst ett av områdene)
        var hasMunicipality = filter.MunicipalityIds?.Any() == true;
        var hasCounty = filter.CountyIds?.Any() == true;
        var hasRestricted = filter.RestrictedAreaIds?.Any() == true;
        var hasOcean = filter.OceanAreaIds?.Any() == true;

        if (hasMunicipality || hasCounty || hasRestricted || hasOcean)
        {
            var municipalityIds = _areaHierarchy.FidsToEntityIds(filter.MunicipalityIds);
            var countyIds = _areaHierarchy.FidsToEntityIds(filter.CountyIds);
            var restrictedIds = _areaHierarchy.RestrictedAreaFidsToEntityIds(filter.RestrictedAreaIds);
            var oceanIds = _areaHierarchy.FidsToEntityIds(filter.OceanAreaIds);

            query = query.Where(o => _context.Set<ObservationEntityIndex>().Any(idx =>
                idx.ObservationId == o.Id && (
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.Municipality && municipalityIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.County && countyIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.RestrictedArea && restrictedIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.OceanArea && oceanIds.Contains(idx.EntityId))
                )));
        }

        // Organisasjonsfilter (AND — separat fra geografiske filtre)
        if (filter.OrganizationIds?.Any() == true)
        {
            var orgIds = filter.OrganizationIds;
            query = query.Where(o => _context.Set<ObservationEntityIndex>().Any(idx =>
                idx.ObservationId == o.Id &&
                idx.EntityTypeId == (int)ObservationIndexEntityType.Institution &&
                orgIds.Contains(idx.EntityId)));
        }

        if (filter.BehaviorIds?.Any() == true)
        {
            query = query.Where(o => o.Behaviors.Any(b => filter.BehaviorIds.Contains(b.Id)));
        }

        if (filter.BasisOfRecordIds?.Any() == true)
        {
            var basisOfRecordIds = filter.BasisOfRecordIds.ToList();
            query = query.Where(o => basisOfRecordIds.Contains(o.BasisOfRecordId));
        }

        if (filter.RegistrationStatusId.HasValue)
        {
            switch (filter.RegistrationStatusId.Value)
            {
                case 1:
                    query = query.Where(o => !o.Tags.Any(t => t.Id == (int)TagEnum.Absent || t.Id == (int)TagEnum.NotRecovered));
                    break;
                case 2:
                    query = query.Where(o => o.Tags.Any(t => t.Id == (int)TagEnum.Absent));
                    break;
                case 3:
                    query = query.Where(o => o.Tags.Any(t => t.Id == (int)TagEnum.NotRecovered));
                    break;
            }
        }

        if (filter.CoordinatePrecision?.From.HasValue == true)
        {
            query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecision.From.Value);
        }

        if (filter.CoordinatePrecision?.To.HasValue == true)
        {
            query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecision.To.Value);
        }

        if (filter.Period?.From.HasValue == true)
        {
            var fromDate = new DateTime(filter.Period.From.Value, 1, 1);
            query = query.Where(o => o.DateTimeCollected >= fromDate);
        }

        if (filter.Period?.To.HasValue == true)
        {
            var toDate = new DateTime(filter.Period.To.Value, 12, 31, 23, 59, 59);
            query = query.Where(o => o.DateTimeCollected <= toDate);
        }

        if (filter.ProjectOrganizationId.HasValue)
        {
            var projectOrganizationId = filter.ProjectOrganizationId.Value;
            query = query.Where(o => o.OrganizationRelations.Any(r => r.OrganizationId == projectOrganizationId));
        }
        else if (!string.IsNullOrWhiteSpace(filter.ProjectName))
        {
            var projectNamePattern = SqlWildcard + filter.ProjectName.Trim().EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => o.OrganizationRelations.Any(r => EF.Functions.Like(r.Organization.Name, projectNamePattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.CollectionCode))
        {
            var collectionCodePattern = SqlWildcard + filter.CollectionCode.Trim().EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => o.CollectionCode != null && EF.Functions.Like(o.CollectionCode, collectionCodePattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.CatalogNumber))
        {
            var catalogNumberPattern = SqlWildcard + filter.CatalogNumber.Trim().EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => o.CatalogNumber != null && EF.Functions.Like(o.CatalogNumber, catalogNumberPattern));
        }

        if (filter.WithImages.HasValue)
        {
            query = filter.WithImages.Value
                ? query.Where(o => o.MediaFiles.Any())
                : query.Where(o => !o.MediaFiles.Any());
        }

        if (filter.Period?.Months?.Any() == true)
        {
            var months = filter.Period.Months;
            query = query.Where(o => o.DateTimeCollected.HasValue && months.Contains(o.DateTimeCollected.Value.Month));
        }

        return query;
    }
    /// <summary>
    /// Henter områdemarkører (fylker/kommuner) gruppert etter navn med observasjonsantall.
    /// Hybrid tilnærming: uten filtre brukes pre-beregnet antall fra Area-tabellen,
    /// med filtre beregnes antall dynamisk via ObservationEntityIndex.
    /// </summary>
    public async Task<IEnumerable<AreaMarkerDto>> GetAreaMarkersAsync(int zoomLevel, LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var areas = await _context.Set<Area>()
                    .Where(a => a.ZoomLevel == zoomLevel && a.IsCurrent == true)
                    .ToListAsync(cancellationToken);

            // Havområder kan ha et annet zoomnivå — last inn separat når de er i filteret
            if (filter?.OceanAreaIds?.Any() == true)
            {
                var loadedFids = areas.Select(a => a.Fid).ToHashSet();
                var missingOceanFids = filter.OceanAreaIds.Where(fid => !loadedFids.Contains(fid)).ToArray();
                if (missingOceanFids.Length > 0)
                {
                    var oceanAreas = await _context.Set<Area>()
                        .Where(a => a.IsCurrent && missingOceanFids.Contains(a.Fid))
                        .ToListAsync(cancellationToken);
                    areas.AddRange(oceanAreas);
                }
            }

            var hasFilters = filter?.HasActiveFilters == true;
            var needsDynamicCounts = hasFilters && filter!.HasObservationAttributeFilters;
            Dictionary<(int entityTypeId, int entityId), int>? dynamicCounts = null;
            Dictionary<string, int>? municipalityCountsByCounty = null;

            // Steg 1: Filtrer hvilke områder som vises (uavhengig av tellemåte)
            var hasAreaSelection = hasFilters && (
                filter!.CountyIds?.Length > 0 ||
                filter.MunicipalityIds?.Length > 0 ||
                filter.OceanAreaIds?.Length > 0);

            if (hasAreaSelection)
            {
                var filtered = FilterAreasBySelection(areas, filter!);
                if (filtered.Count > 0)
                {
                    areas = filtered;
                }
                else if (!needsDynamicCounts)
                {
                    // Ingen områder matcher og ingen observasjonsfiltre — ingen markører å vise
                    areas = filtered;
                }
            }

            // Steg 2: Bestem tellemåte
            if (needsDynamicCounts)
            {
                dynamicCounts = await ComputeFilteredAreaCounts(areas, filter!, cancellationToken);
            }
            else if (hasAreaSelection)
            {
                // Sjekk om kommune-filter på fylkesnivå krever aggregering
                var hasMunicipalityFilter = filter!.MunicipalityIds?.Length > 0;
                var areasAreCounties = areas.Count > 0 && !areas.Any(a => filter.MunicipalityIds?.Contains(a.Fid) == true);

                if (hasMunicipalityFilter && areasAreCounties)
                {
                    municipalityCountsByCounty = await AggregateMunicipalityCountsByCounty(
                        filter.MunicipalityIds!, cancellationToken);
                }
            }

            return areas
                .GroupBy(a => a.Name)
                .Select(g =>
                {
                    var firstArea = g.FirstOrDefault(a => a.WktPolygon != null) ?? g.First();

                    int count;
                    if (needsDynamicCounts)
                    {
                        count = g.Sum(a =>
                        {
                            var entityId = _areaHierarchy.FidToEntityId(a.Fid);
                            return entityId.HasValue
                                ? dynamicCounts!.GetValueOrDefault((a.AreaTypeId, entityId.Value), 0)
                                : 0;
                        });
                    }
                    else if (municipalityCountsByCounty != null)
                    {
                        count = g.Sum(a => municipalityCountsByCounty.GetValueOrDefault(a.Fid, 0));
                    }
                    else
                    {
                        count = g.Sum(a => a.ObservationCount ?? 0);
                    }

                    return new AreaMarkerDto
                    {
                        Id = g.Min(a => a.Id),
                        DocumentId = firstArea.DocumentId,
                        Fid = firstArea.Fid,
                        Name = g.Key,
                        AreaTypeId = firstArea.AreaTypeId,
                        ParentFid = firstArea.ParentFid,
                        ObservationCount = count,
                        WktsPolygon = firstArea.WktPolygon?.AsText(),
                        Centroid = firstArea.Centroid?.Coordinate != null
                            ? new CentroidDto { X = firstArea.Centroid.Coordinate.X, Y = firstArea.Centroid.Coordinate.Y }
                            : null
                    };
                })
                .Where(a => a.ObservationCount > 0)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error retrieving areas for zoom level: {ZoomLevel}", zoomLevel);
            throw new ApplicationException(
                "An error occurred while retrieving areas. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Henter pre-beregnede observasjonsantall for valgte kommuner og aggregerer per forelder-fylke.
    /// Brukes når kommune-filter er aktivt på fylkesnivå — unngår tung observasjonstelling.
    /// </summary>
    private async Task<Dictionary<string, int>> AggregateMunicipalityCountsByCounty(
        string[] municipalityFids, CancellationToken cancellationToken)
    {
        var fidsSet = municipalityFids.ToHashSet();

        var municipalityAreas = await _context.Set<Area>()
            .Where(a => a.IsCurrent && fidsSet.Contains(a.Fid))
            .Select(a => new { a.Fid, a.ParentFid, a.ObservationCount })
            .ToListAsync(cancellationToken);

        var countsByCounty = new Dictionary<string, int>();

        foreach (var m in municipalityAreas)
        {
            var countyFid = m.ParentFid ?? _areaHierarchy.GetCountyFid(m.Fid);
            if (countyFid == null) continue;

            countsByCounty.TryGetValue(countyFid, out var current);
            countsByCounty[countyFid] = current + (m.ObservationCount ?? 0);
        }

        return countsByCounty;
    }

    /// <summary>
    /// Beregner filtrerte observasjonsantall per område via ObservationEntityIndex.
    /// </summary>
    private async Task<Dictionary<(int entityTypeId, int entityId), int>> ComputeFilteredAreaCounts(
        List<Area> areas, LocationSearchFilterDto filter, CancellationToken cancellationToken)
    {
        var filteredQuery = ApplyCommonFilters(_context.Set<Observation>().AsNoTracking(), filter);

        var entityTypeIds = areas.Select(a => a.AreaTypeId).Distinct().ToArray();
        var entityIds = areas
            .Select(a => _areaHierarchy.FidToEntityId(a.Fid))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var counts = await _context.Set<ObservationEntityIndex>()
            .Where(idx => entityTypeIds.Contains(idx.EntityTypeId) && entityIds.Contains(idx.EntityId))
            .Where(idx => filteredQuery.Select(o => o.Id).Contains(idx.ObservationId))
            .GroupBy(idx => new { idx.EntityTypeId, idx.EntityId })
            .Select(g => new { g.Key.EntityTypeId, g.Key.EntityId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => (x.EntityTypeId, x.EntityId), x => x.Count);
    }

    /// <summary>
    /// Filtrerer områdelisten basert på valgte fylker, kommuner og havområder.
    /// Alle er områdemarkører på samme nivå og filtreres via Fid-matching.
    /// Bruker AreaHierarchyService for å slå opp forelder-fylke fra kommune-Fid.
    /// </summary>
    private List<Area> FilterAreasBySelection(List<Area> areas, LocationSearchFilterDto filter)
    {
        var countyFids = new HashSet<string>(filter.CountyIds ?? []);
        var municipalityFids = new HashSet<string>(filter.MunicipalityIds ?? []);
        var oceanAreaFids = new HashSet<string>(filter.OceanAreaIds ?? []);

        if (countyFids.Count == 0 && municipalityFids.Count == 0 && oceanAreaFids.Count == 0)
            return areas;

        // Slå opp forelder-fylke for valgte kommuner
        var derivedCountyFids = new HashSet<string>();
        foreach (var mFid in municipalityFids)
        {
            var parentCounty = _areaHierarchy.GetCountyFid(mFid);
            if (parentCounty != null)
                derivedCountyFids.Add(parentCounty);
        }

        return areas.Where(a =>
            countyFids.Contains(a.Fid) ||
            municipalityFids.Contains(a.Fid) ||
            oceanAreaFids.Contains(a.Fid) ||
            (a.ParentFid != null && countyFids.Contains(a.ParentFid)) ||
            derivedCountyFids.Contains(a.Fid)
        ).ToList();
    }

    private IQueryable<Observation> BuildLocationsQuery(LocationSearchFilterDto filter)
    {
        var query = _context.Set<Observation>().AsNoTracking();
        query = ApplyCommonFilters(query, filter);

        if (filter.Envelope != null)
        {
            var minX = (int)filter.Envelope.MinX;
            var maxX = (int)filter.Envelope.MaxX;
            var minY = (int)filter.Envelope.MinY;
            var maxY = (int)filter.Envelope.MaxY;
            query = query.Where(o => o.Location != null &&
                o.Location.East >= minX && o.Location.East <= maxX &&
                o.Location.North >= minY && o.Location.North <= maxY);
        }

        return query;
    }

    private async Task<List<(int LocationId, int ObservationCount)>> AggregateLocationObservations(
        IQueryable<Observation> query, int maxResults, CancellationToken cancellationToken)
    {
        var results = await query
            .Where(o => o.LocationId != null)
            .GroupBy(o => o.LocationId!.Value)
            .Select(g => new { LocationId = g.Key, ObservationCount = g.Count() })
            .OrderByDescending(x => x.ObservationCount)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return results.Select(x => (x.LocationId, x.ObservationCount)).ToList();
    }

    /// <summary>
    /// Henter polygon-geometrier fra Location-tabellen for observasjoner som matcher filteret.
    /// Rektangulære polygoner (nøyaktig 5 punkter i ytre ring = rutenett-firkanter) filtreres bort.
    /// </summary>
    public async Task<IEnumerable<LocationPolygonDto>> GetLocationPolygonsAsync(LocationSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            filter ??= new LocationSearchFilterDto();

            var query = BuildLocationsQuery(filter);
            var polygonMaxResults = filter.MaxResults > 0
                ? Math.Min(filter.MaxResults, SearchConstants.MaxPolygonResults)
                : SearchConstants.DefaultMaxPolygons;
            var aggregated = await AggregateLocationObservations(query, polygonMaxResults, cancellationToken);

            if (aggregated.Count == 0) return [];

            var locationIds = aggregated.Select(x => x.LocationId).ToList();

            var locations = await _context.Set<Location>()
                .AsNoTracking()
                .Where(l => locationIds.Contains(l.Id) && l.Geometry != null && (l.Geometry.GeometryType == "Polygon" || l.Geometry.GeometryType == "MultiPolygon"))
                .Select(l => new { l.Id, l.Locality, l.Geometry })
                .ToListAsync(cancellationToken);

            if (locations.Count == 0) return [];

            var countLookup = aggregated.ToDictionary(x => x.LocationId, x => x.ObservationCount);

            var result = new List<LocationPolygonDto>();

            foreach (var location in locations)
            {
                var geo = location.Geometry!;
                var wkt = geo.AsText();
                if (IsRectangularPolygon(wkt)) continue;

                // Skip polygons whose bounding box doesn't intersect the visible map extent
                if (filter.Envelope != null)
                {
                    var env = geo.EnvelopeInternal;
                    if (env.MaxX < filter.Envelope.MinX || env.MinX > filter.Envelope.MaxX ||
                        env.MaxY < filter.Envelope.MinY || env.MinY > filter.Envelope.MaxY)
                        continue;
                }

                result.Add(new LocationPolygonDto
                {
                    LocationId = location.Id,
                    Locality = location.Locality,
                    WktPolygon = wkt,
                    ObservationCount = countLookup.GetValueOrDefault(location.Id)
                });
            }

            _logger.LogInformation("Location polygon search completed. Returned {Count} polygons", result.Count);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Feil ved henting av lokasjonspolygoner");
            throw new ApplicationException("An error occurred while retrieving location polygons. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Returns true when the WKT string represents a rectangular polygon (exactly 5 coordinate pairs in the exterior ring).
    /// </summary>
    private static bool IsRectangularPolygon(string? wkt)
    {
        if (string.IsNullOrEmpty(wkt)) return false;
        var ringStart = wkt.IndexOf('(', wkt.IndexOf('(') + 1);
        var ringEnd = wkt.IndexOf(')', ringStart);
        if (ringStart < 0 || ringEnd < 0) return false;

        var ring = wkt.AsSpan(ringStart + 1, ringEnd - ringStart - 1);
        var commaCount = 0;
        foreach (var ch in ring)
        {
            if (ch == ',') commaCount++;
        }

        return commaCount == 4;
    }

}

using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Enums;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Oppslag for områdehierarki (kommune→fylke) og Fid→EntityId-konvertering.
/// Laster data fra Area-tabellen ved oppstart og oppdaterer periodisk.
/// </summary>
public class AreaHierarchyService : IAreaHierarchyService, IHostedService, IDisposable
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Slingringsmonn på områdeboksene, i meter.
    ///
    /// Boksene er utledet av data og lastes på nytt hver time. Kommer det nye
    /// observasjoner som utvider et område i mellomtiden, kunne en for stram boks
    /// fått oss til å utelate et område som faktisk har treff. Marginen dekker det
    /// vinduet, og samtidig at områdene er lagret i EPSG:32633 mens utsnittet
    /// kommer i 25833 — omtrent én meter fra hverandre.
    ///
    /// En km koster oss ingenting: den gjør bare at optimaliseringen ikke slår inn
    /// for utsnitt som ligger nærmere enn det fra områdets ytterkant.
    /// </summary>
    private const int BoundsMarginMetres = 1000;

    private volatile Dictionary<string, string> _municipalityToCounty = new();
    private volatile Dictionary<string, List<string>> _countyToMunicipalities = new();
    private volatile Dictionary<(int EntityTypeId, int EntityId), AreaBounds> _areaBounds = new();
    private volatile bool _initialized;
    private PeriodicTimer? _timer;
    private Task? _backgroundTask;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AreaHierarchyService> _logger;

    public AreaHierarchyService(IServiceScopeFactory scopeFactory, ILogger<AreaHierarchyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        _timer = new PeriodicTimer(ReloadInterval);
        _backgroundTask = ReloadLoopAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        if (_backgroundTask != null)
            await _backgroundTask;
    }

    private async Task ReloadLoopAsync()
    {
        while (_timer != null && await _timer.WaitForNextTickAsync())
        {
            try
            {
                await LoadAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feil ved periodisk oppdatering av områdehierarki");
            }
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        var areas = await context.Set<Area>()
            .Where(a => a.IsCurrent)
            .Select(a => new { a.Fid, a.AreaTypeId, a.ParentFid })
            .Distinct()
            .ToListAsync(cancellationToken);

        var municipalityToCounty = new Dictionary<string, string>();
        var countyToMunicipalities = new Dictionary<string, List<string>>();

        // AreaTypeId 1 = Kommune
        foreach (var m in areas.Where(a => a.AreaTypeId == 1))
        {
            if (string.IsNullOrEmpty(m.ParentFid)) continue;

            municipalityToCounty[m.Fid] = m.ParentFid;

            if (!countyToMunicipalities.TryGetValue(m.ParentFid, out var list))
            {
                list = [];
                countyToMunicipalities[m.ParentFid] = list;
            }

            if (!list.Contains(m.Fid))
                list.Add(m.Fid);
        }

        var areaBounds = await LoadAreaBoundsAsync(context, cancellationToken);

        // Volatile swap — hver referanse settes atomisk, men de tre tilordningene er ikke
        // atomiske samlet. En leser kan i et kort øyeblikk se ny _municipalityToCounty
        // med gammel _countyToMunicipalities. Akseptabelt fordi data sjelden endres
        // og en enkelt forespørsel typisk bruker bare én av oppslagene.
        _municipalityToCounty = municipalityToCounty;
        _countyToMunicipalities = countyToMunicipalities;
        _areaBounds = areaBounds;
        _initialized = true;

        _logger.LogInformation(
            "Lastet områdehierarki: {MunicipalityCount} kommuner, {CountyCount} fylker, {BoundsCount} områdebokser",
            municipalityToCounty.Count, countyToMunicipalities.Count, areaBounds.Count);
    }

    /// <summary>
    /// Utstrekningen til observasjonslokasjonene i hvert område.
    ///
    /// HVORFOR IKKE Area.Bbox
    /// Area-tabellen har allerede en Bbox-kolonne, men den beskriver polygonet, og
    /// observasjonene følger det ikke. Målt mot prodlik database ligger 356 239
    /// indeksrader for verneområder utenfor sitt eget polygons bbox — opptil 78 km
    /// utenfor — pluss 5 759 for kommuner og 89 for fylker. Å luke på polygonets
    /// boks ville dermed fjernet ekte treff.
    ///
    /// Boksen rundt lokasjonene svarer på nøyaktig det spørsmålet vi stiller: kan
    /// en observasjon i dette området ha en lokasjon i kartutsnittet? Per
    /// konstruksjon ligger ingen lokasjon utenfor.
    ///
    /// Prisen er at én feilregistrert koordinat blåser opp boksen for hele
    /// området. 15 av 357 kommuner og 2 av 15 fylker er slik — Larvik har polygon
    /// på 30 × 83 km, men lokasjoner spredt over 695 × 1323 km. For dem slår
    /// optimaliseringen aldri inn. Svaret blir like riktig, bare ikke raskere.
    ///
    /// Aggregeringen er målt til 2,6 sekunder over 134 millioner indeksrader, så
    /// den tas ved oppstart og ved hver periodiske oppdatering.
    /// </summary>
    private static async Task<Dictionary<(int, int), AreaBounds>> LoadAreaBoundsAsync(
        IArtsKartDbContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Set<ObservationEntityIndex>()
            .AsNoTracking()
            .Where(i => i.EntityTypeId != (int)ObservationIndexEntityType.Institution && i.LocationId != null)
            .Join(context.Set<Location>(),
                i => i.LocationId, l => l.Id,
                (i, l) => new { i.EntityTypeId, i.EntityId, l.East, l.North })
            .GroupBy(x => new { x.EntityTypeId, x.EntityId })
            .Select(g => new
            {
                g.Key.EntityTypeId,
                g.Key.EntityId,
                MinX = g.Min(x => x.East),
                MaxX = g.Max(x => x.East),
                MinY = g.Min(x => x.North),
                MaxY = g.Max(x => x.North)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => (r.EntityTypeId, r.EntityId),
            r => new AreaBounds(r.MinX, r.MinY, r.MaxX, r.MaxY));
    }

    public AreaBounds? GetAreaBounds(int entityTypeId, int entityId)
        => _areaBounds.TryGetValue((entityTypeId, entityId), out var b) ? b : null;

    public int[] PruneToEnvelope(int[] entityIds, AreaBounds envelope, params int[] entityTypeIds)
        => AreaBoundsPruner.Prune(entityIds, envelope.Expand(BoundsMarginMetres), _areaBounds, entityTypeIds);

    /// <inheritdoc />
    public int[] FilterToExistingAreas(int[] entityIds, int entityTypeId)
    {
        if (entityIds.Length == 0) return entityIds;

        // Boksene er ikke lastet ennå. Da vet vi ingenting, og «vet ikke» skal
        // gi med grenen — ikke uten. Se grensesnittet.
        var bounds = _areaBounds;
        if (bounds.Count == 0) return entityIds;

        return entityIds.Where(id => bounds.ContainsKey((entityTypeId, id))).ToArray();
    }

    public string? GetCountyFid(string municipalityFid)
    {
        EnsureInitialized();
        return _municipalityToCounty.GetValueOrDefault(municipalityFid);
    }

    public IReadOnlyList<string> GetMunicipalityFids(string countyFid)
    {
        EnsureInitialized();
        return _countyToMunicipalities.TryGetValue(countyFid, out var list)
            ? list.AsReadOnly()
            : [];
    }

    public int? FidToEntityId(string fid)
    {
        return int.TryParse(fid.Replace("_", ""), out var id) ? id : null;
    }

    public int? RestrictedAreaFidToEntityId(string fid)
    {
        return int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : null;
    }

    public int[] FidsToEntityIds(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(FidToEntityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    public int[] RestrictedAreaFidsToEntityIds(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(RestrictedAreaFidToEntityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("AreaHierarchyService er ikke initialisert.");
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

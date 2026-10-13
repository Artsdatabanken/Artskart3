using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Laster taksonomien fra indeksbasen: taksoner, navn, ranger og egenskaper.
///
/// FIRE SPØRRINGER, INGEN JOINER OVER STORE TABELLER. Navnene trenger rangen, som
/// ligger på taksonet — men en join ville gjentatt taksonraden per navn. Her hentes
/// hver tabell for seg og settes sammen i minnet.
///
/// SLETTEDE RADER UTELATES, slik Artskart 2s spørring gjør
/// (<c>tn.IsDeleted = 0 AND t.IsDeleted = 0</c>).
/// </summary>
public class EfTaxonomySource : ITaxonomySource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfTaxonomySource> _logger;

    public EfTaxonomySource(IServiceScopeFactory scopeFactory, ILogger<EfTaxonomySource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public TaxonomySnapshot LoadAll()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

        var ranger = context.Set<TaxonRank>()
            .AsNoTracking()
            .Select(x => new { x.Id, x.Name })
            .ToDictionary(x => x.Id, x => (x.Name ?? string.Empty).ToLowerInvariant());

        var taksoner = context.Set<Taxon>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.TaxonRankId,
                x.ValidScientificName,
                x.ValidScientificNameAuthorship,
                x.TaxonGroupId,
                x.TaxonIdHiarchy
            })
            .ToList();

        var navn = context.Set<TaxonName>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.TaxonId != null)
            .Select(x => new
            {
                x.Id,
                TaxonId = x.TaxonId!.Value,
                x.Accepted,
                x.ScientificName,
                x.ScientificNameAuthorship
            })
            .ToList();

        var egenskaper = context.Set<TaxonProperty>()
            .AsNoTracking()
            .Where(x => x.TaxonId != null)
            .Select(x => new
            {
                TaxonId = x.TaxonId!.Value,
                x.Prefix,
                x.Context,
                x.Tag
            })
            .ToList();

        var egenskaperPåTakson = egenskaper
            .GroupBy(x => x.TaxonId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<TaxonPropertyInfo>)x
                    .Select(y => new TaxonPropertyInfo(y.Prefix, y.Context, y.Tag))
                    .ToList());

        var navneIderPåTakson = navn
            .GroupBy(x => x.TaxonId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToArray());

        var rangPåTakson = taksoner.ToDictionary(x => x.Id, x => x.TaxonRankId);

        var taxaResultat = taksoner
            .Select(x => new TaxonRecord(
                x.Id,
                x.TaxonRankId,
                x.ValidScientificName,
                x.ValidScientificNameAuthorship,
                x.TaxonGroupId,
                TolkHierarki(x.TaxonIdHiarchy),
                navneIderPåTakson.TryGetValue(x.Id, out var ider) ? ider : [],
                egenskaperPåTakson.TryGetValue(x.Id, out var props) ? props : []))
            .ToList();

        var navnResultat = navn
            .Where(x => rangPåTakson.ContainsKey(x.TaxonId))
            .Select(x =>
            {
                var rangId = rangPåTakson[x.TaxonId];

                return new TaxonNameEntry
                {
                    NameId = x.Id,
                    TaxonId = x.TaxonId,
                    Accepted = x.Accepted,
                    ScientificNameFormatted = x.ScientificName,
                    ScientificNameAuthorship = x.ScientificNameAuthorship,
                    RankId = rangId,
                    RankName = ranger.TryGetValue(rangId, out var rangNavn) ? rangNavn : string.Empty
                };
            })
            .ToList();

        _logger.LogInformation(
            "Leste taksonomien: {TaxonCount} taksoner, {NameCount} navn, {PropertyCount} egenskaper",
            taxaResultat.Count, navnResultat.Count, egenskaper.Count);

        return new TaxonomySnapshot(taxaResultat, navnResultat);
    }

    /// <summary>
    /// <c>TaxonIdHiarchy</c> er kommaseparert med taksonet selv først og roten sist:
    /// «1571,1570,1569,…,1». Artskart 2 bruker <c>Convert.ToInt32</c> rett på hvert
    /// ledd og ville kastet på en tom verdi. Målt: 0 av 152 756 taksoner har tomt
    /// hierarki — vernet er for dataene som kan komme.
    /// </summary>
    private static int[] TolkHierarki(string? hierarki)
    {
        if (string.IsNullOrWhiteSpace(hierarki)) return [];

        return hierarki
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x != 0)
            .ToArray();
    }
}

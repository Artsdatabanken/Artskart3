using Artskart3.Core.Application.Taxonomy;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Speiler taksonomien fra Artskart 2s dokumentlager inn i Artskart 3s SQL Server.
///
/// FULL REFRESH, IKKE INKREMENTELT. Artskart 2 har to veier: en masselast
/// (<c>TaxonImporter.AddTaxons</c>) og en hendelsesvask
/// (<c>TaxonomyMaintenanceService.ProcessTaxonChanges</c>) med vannmerke i
/// <c>TaxonomyState</c>. Vasken gjør to jobber samtidig — den oppdaterer taksonomien OG
/// re-matcher eksisterende observasjoner. Her gjøres bare den første: 180 000 taksoner
/// er billig å lese om, så vannmerket, hendelsesstrømmen og batchene på 128 er ikke
/// portert. Re-matching av observasjoner er en reprosesseringssak og hører sammen med
/// annotasjonene.
///
/// UPSERT, ALDRI HARDSLETTING. Det finnes fremmednøkler <c>Observation → Taxon</c> og
/// <c>Observation → TaxonName</c>, så rader kan ikke slettes og settes inn på nytt.
/// Artskart 2 sier det samme om sine egne data: «Taxon is kept for history - in case
/// records still points towards it». <c>IsDeleted</c> følger derfor kildens flagg, og
/// rader som er borte fra kilden blir stående.
///
/// RANGENE KOMMER IKKE FRA RAVENDB. Artskart 2 fyller <c>TaxonRank</c> fra sin egen
/// <c>Category</c>-enum, og <see cref="TaxonomicCategory"/> er den samme listen.
/// </summary>
public sealed class TaxonomyRefreshService
{
    private readonly ITaxonomySource _kilde;
    private readonly ArtskartDbContext _db;
    private readonly TaxonomyOptions _innstillinger;
    private readonly ILogger<TaxonomyRefreshService> _logger;
    private readonly TimeProvider _klokke;

    public TaxonomyRefreshService(
        ITaxonomySource kilde,
        ArtskartDbContext db,
        TaxonomyOptions innstillinger,
        ILogger<TaxonomyRefreshService> logger,
        TimeProvider? klokke = null)
    {
        _kilde = kilde;
        _db = db;
        _innstillinger = innstillinger;
        _logger = logger;
        _klokke = klokke ?? TimeProvider.System;
    }

    public async Task<TaxonomyRefreshResult> RefreshAsync(CancellationToken cancellationToken)
    {
        var start = _klokke.GetUtcNow();

        _logger.LogInformation("Speiler taksonomien fra {Kilde}", _kilde.Describe());

        var ranger = await OppdaterRanger(cancellationToken);
        var grupper = await OppdaterGrupper(cancellationToken);
        var (taksoner, navn, populærnavn, egenskaper) = await OppdaterTaksoner(cancellationToken);

        var resultat = new TaxonomyRefreshResult(
            _kilde.Describe(), ranger, grupper, taksoner, navn, populærnavn, egenskaper,
            _klokke.GetUtcNow() - start);

        _logger.LogInformation(
            "Taksonomien er speilet: {Ranger} ranger, {Grupper} grupper, {Taksoner} taksoner, "
            + "{Navn} navn, {Populaernavn} populærnavn, {Egenskaper} egenskaper på {Sekunder} s",
            resultat.Ranks, resultat.Groups, resultat.Taxa, resultat.Names,
            resultat.PopularNames, resultat.Properties,
            Math.Round(resultat.Elapsed.TotalSeconds, 1));

        return resultat;
    }

    /// <summary>
    /// Rangene fra enumen. <c>NotSet</c> tas med, slik Artskart 2 gjør — den har en
    /// egen rad der også.
    /// </summary>
    private async Task<int> OppdaterRanger(CancellationToken cancellationToken)
    {
        var eksisterende = await _db.TaxonRanks.ToDictionaryAsync(x => x.Id, cancellationToken);
        var nå = _klokke.GetUtcNow().UtcDateTime;
        var antall = 0;

        foreach (var rang in Enum.GetValues<TaxonomicCategory>())
        {
            var navn = rang.ToString().ToLowerInvariant();

            if (eksisterende.TryGetValue((int)rang, out var rad))
            {
                if (rad.Name == navn) continue;

                rad.Name = navn;
                rad.UpdatedAt = nå;
            }
            else
            {
                _db.TaxonRanks.Add(new TaxonRank { Id = (int)rang, Name = navn, CreatedAt = nå, UpdatedAt = nå });
            }

            antall++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return antall;
    }

    private async Task<int> OppdaterGrupper(CancellationToken cancellationToken)
    {
        var fraKilden = await _kilde.ReadSpeciesGroupsAsync(cancellationToken);
        var eksisterende = await _db.TaxonGroups.ToDictionaryAsync(x => x.Id, cancellationToken);
        var nå = _klokke.GetUtcNow().UtcDateTime;
        var antall = 0;

        foreach (var gruppe in fraKilden)
        {
            var navn = gruppe.PresentationName ?? string.Empty;

            if (eksisterende.TryGetValue(gruppe.Id, out var rad))
            {
                if (rad.Name == navn) continue;

                rad.Name = navn;
                rad.UpdatedAt = nå;
            }
            else
            {
                _db.TaxonGroups.Add(new TaxonGroup { Id = gruppe.Id, Name = navn, CreatedAt = nå, UpdatedAt = nå });
            }

            antall++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return antall;
    }

    /// <summary>
    /// Taksonene med navn, populærnavn og egenskaper, i bunter.
    ///
    /// TAKSONET MÅ SKRIVES FØR BARNA. <c>TaxonName</c>, <c>TaxonPopularName</c> og
    /// <c>TaxonProperty</c> har alle fremmednøkkel til <c>Taxon</c>, så bunten lagres i
    /// to omganger.
    /// </summary>
    private async Task<(int Taksoner, int Navn, int Populærnavn, int Egenskaper)> OppdaterTaksoner(
        CancellationToken cancellationToken)
    {
        var bunt = new List<TaxonInfoDocument>(_innstillinger.BatchSize);
        int taksoner = 0, navn = 0, populærnavn = 0, egenskaper = 0;

        var hoppetOver = 0;

        await foreach (var dokument in _kilde.ReadTaxaAsync(cancellationToken))
        {
            // SLETTEDE TAKSONER SKAL IKKE INN. Målt mot Artskart 2s fasit: den har 0
            // taksoner med IsDeleted og 0 navn som peker på et slettet takson, mens
            // dokumentlageret har 43 949 slettede av 196 767.
            //
            // Og det er ikke kosmetikk. Et slettet taksons dokument lister også sine
            // navn, og et navn kan stå under både det gamle og det nye taksonet. Tas de
            // slettede med, overskriver de navnet med sitt eget takson, sin egen
            // Accepted og sin egen navnestreng — og da peker oppslaget en observasjon mot
            // et takson som ikke finnes lenger. Første kjøring uten dette filteret ga
            // 16 402 navn mot slettede taksoner, og ALLE 13 170 navnestreng-avvikene,
            // ALLE 4 608 Accepted-avvikene og ALLE 15 486 taksonavvikene mot Artskart 2
            // lå på nettopp dem.
            //
            // Rader som alt står i basen blir stående — speilingen sletter aldri.
            if (dokument.IsDeleted && _innstillinger.SkipDeletedTaxa)
            {
                hoppetOver++;
                continue;
            }

            bunt.Add(dokument);

            if (bunt.Count < _innstillinger.BatchSize) continue;

            var tall = await SkrivBunt(bunt, cancellationToken);
            taksoner += tall.Taksoner;
            navn += tall.Navn;
            populærnavn += tall.Populærnavn;
            egenskaper += tall.Egenskaper;

            _logger.LogInformation("Speilet {Antall} taksoner", taksoner);

            bunt.Clear();
        }

        if (bunt.Count > 0)
        {
            var tall = await SkrivBunt(bunt, cancellationToken);
            taksoner += tall.Taksoner;
            navn += tall.Navn;
            populærnavn += tall.Populærnavn;
            egenskaper += tall.Egenskaper;
        }

        if (hoppetOver > 0)
        {
            _logger.LogInformation(
                "Hoppet over {Antall} slettede taksoner, slik Artskart 2s indeks ikke har dem",
                hoppetOver);
        }

        return (taksoner, navn, populærnavn, egenskaper);
    }

    private async Task<(int Taksoner, int Navn, int Populærnavn, int Egenskaper)> SkrivBunt(
        List<TaxonInfoDocument> bunt,
        CancellationToken cancellationToken)
    {
        var nå = _klokke.GetUtcNow().UtcDateTime;
        var ider = bunt.Select(x => x.Id).ToArray();

        var eksisterende = await _db.Taxons
            .Where(x => ider.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var dokument in bunt)
        {
            if (eksisterende.TryGetValue(dokument.Id, out var rad))
            {
                TaxonomyMapping.Apply(rad, dokument, nå);
            }
            else
            {
                var ny = new Taxon { Id = dokument.Id, CreatedAt = nå };
                TaxonomyMapping.Apply(ny, dokument, nå);
                _db.Taxons.Add(ny);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var navn = await SkrivNavn(bunt, nå, cancellationToken);
        var populærnavn = await SkrivPopulærnavn(bunt, ider, nå, cancellationToken);
        var egenskaper = await SkrivEgenskaper(bunt, ider, nå, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _db.ChangeTracker.Clear();

        return (bunt.Count, navn, populærnavn, egenskaper);
    }

    /// <summary>
    /// Navnene upsertes på <c>ScientificNameId</c> — Artsnavnebasens id, som er
    /// <c>TaxonName.Id</c> i indeksen og ikke en autogenerert nøkkel.
    /// </summary>
    private async Task<int> SkrivNavn(
        List<TaxonInfoDocument> bunt, DateTime nå, CancellationToken cancellationToken)
    {
        var fraKilden = bunt
            .SelectMany(t => t.ScientificNames.Select(n => (Takson: t.Id, Navn: n)))
            .GroupBy(x => x.Navn.ScientificNameId)
            .Select(g => g.First())
            .ToList();

        var ider = fraKilden.Select(x => x.Navn.ScientificNameId).ToArray();

        var eksisterende = await _db.TaxonNames
            .Where(x => ider.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var (takson, navn) in fraKilden)
        {
            if (eksisterende.TryGetValue(navn.ScientificNameId, out var rad))
            {
                TaxonomyMapping.Apply(rad, navn, takson, nå);
            }
            else
            {
                var ny = new TaxonName { Id = navn.ScientificNameId, CreatedAt = nå };
                TaxonomyMapping.Apply(ny, navn, takson, nå);
                _db.TaxonNames.Add(ny);
            }
        }

        return fraKilden.Count;
    }

    /// <summary>
    /// Populærnavn og egenskaper erstattes i sin helhet for taksonene i bunten.
    ///
    /// DET ER TRYGT HER, i motsetning til for taksoner og navn: ingenting peker på disse
    /// to tabellene. Fremmednøklene går bare ut, til <c>Taxon</c>. Da er sletting og ny
    /// innsetting både enklere og riktigere enn å gjette på en naturlig nøkkel —
    /// <c>TaxonProperty</c> har ingen.
    ///
    /// ARTSKART 2 FILTRERER BORT POPULÆRNAVN UTEN SPRÅK. Bevart.
    /// </summary>
    private async Task<int> SkrivPopulærnavn(
        List<TaxonInfoDocument> bunt, int[] ider, DateTime nå, CancellationToken cancellationToken)
    {
        await _db.TaxonPopularNames
            .Where(x => x.TaxonId != null && ider.Contains(x.TaxonId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        var nye = bunt
            .SelectMany(t => t.PopularNames
                .Where(p => p.language is not null)
                .Select(p => TaxonomyMapping.ToPopularName(p, t.Id, nå)))
            .ToList();

        _db.TaxonPopularNames.AddRange(nye);

        return nye.Count;
    }

    private async Task<int> SkrivEgenskaper(
        List<TaxonInfoDocument> bunt, int[] ider, DateTime nå, CancellationToken cancellationToken)
    {
        await _db.TaxonProperties
            .Where(x => x.TaxonId != null && ider.Contains(x.TaxonId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        var nye = bunt
            .SelectMany(t => t.TaxonTags.Select(m => TaxonomyMapping.ToProperty(m, t.Id, nå)))
            .ToList();

        _db.TaxonProperties.AddRange(nye);

        return nye.Count;
    }
}

/// <param name="Source">Hvor dataene kom fra, til bokføring.</param>
/// <param name="Ranks">Ranger lagt til eller endret.</param>
/// <param name="Groups">Artsgrupper lagt til eller endret.</param>
/// <param name="Taxa">Taksoner speilet.</param>
/// <param name="Names">Vitenskapelige navn speilet.</param>
/// <param name="PopularNames">Populærnavn skrevet.</param>
/// <param name="Properties">Taksonegenskaper skrevet.</param>
/// <param name="Elapsed">Tiden speilingen tok.</param>
public sealed record TaxonomyRefreshResult(
    string Source,
    int Ranks,
    int Groups,
    int Taxa,
    int Names,
    int PopularNames,
    int Properties,
    TimeSpan Elapsed);

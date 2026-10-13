using System.Diagnostics;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Måler navneoppslaget mot hele den ekte taksonomien: lastetid, minne, oppslagstid og
/// treffrate på navn som faktisk finnes i basen.
///
/// AVSLÅTT SOM STANDARD:
///
///   $env:ARTSKART_INDEX_CONNECTION = "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~TaxonNameResolverSmoke"
///
/// Trenger ikke Docker. Kun lesing.
/// </summary>
public class TaxonNameResolverSmokeTests
{
    private const string ConnectionVariable = "ARTSKART_INDEX_CONNECTION";

    private readonly ITestOutputHelper _output;

    public TaxonNameResolverSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

    private static ArtskartDbContext NyKontekst(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.UseNetTopologySuite();
                sql.CommandTimeout(600);
            })
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ArtskartDbContext(options);
    }

    /// <summary>Laster taksonomien rett fra basen, uten DI.</summary>
    private sealed class DirekteKilde(string connectionString) : ITaxonomySource
    {
        public TaxonomySnapshot LoadAll()
        {
            using var context = NyKontekst(connectionString);

            var ranger = context.Set<TaxonRank>().AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToDictionary(x => x.Id, x => (x.Name ?? string.Empty).ToLowerInvariant());

            var taksoner = context.Set<Taxon>().AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => new
                {
                    x.Id, x.TaxonRankId, x.ValidScientificName, x.ValidScientificNameAuthorship,
                    x.TaxonGroupId, x.TaxonIdHiarchy
                })
                .ToList();

            var navn = context.Set<TaxonName>().AsNoTracking()
                .Where(x => !x.IsDeleted && x.TaxonId != null)
                .Select(x => new
                {
                    x.Id, TaxonId = x.TaxonId!.Value, x.Accepted, x.ScientificName, x.ScientificNameAuthorship
                })
                .ToList();

            var rangPåTakson = taksoner.ToDictionary(x => x.Id, x => x.TaxonRankId);

            var taxaResultat = taksoner.Select(x => new TaxonRecord(
                x.Id, x.TaxonRankId, x.ValidScientificName, x.ValidScientificNameAuthorship, x.TaxonGroupId,
                (x.TaxonIdHiarchy ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(y => int.TryParse(y, out var id) ? id : 0).Where(y => y != 0).ToArray(),
                [], [])).ToList();

            var navnResultat = navn
                .Where(x => rangPåTakson.ContainsKey(x.TaxonId))
                .Select(x => new TaxonNameEntry
                {
                    NameId = x.Id,
                    TaxonId = x.TaxonId,
                    Accepted = x.Accepted,
                    ScientificNameFormatted = x.ScientificName,
                    ScientificNameAuthorship = x.ScientificNameAuthorship,
                    RankId = rangPåTakson[x.TaxonId],
                    RankName = ranger.TryGetValue(rangPåTakson[x.TaxonId], out var r) ? r : string.Empty
                })
                .ToList();

            return new TaxonomySnapshot(taxaResultat, navnResultat);
        }
    }

    [Fact]
    public void Maaler_lasting_minne_og_oppslagstid()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var oppslag = new TaxonNameResolver(
            new DirekteKilde(connectionString), NullLogger<TaxonNameResolver>.Instance);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var førMinne = GC.GetTotalMemory(forceFullCollection: true);

        var klokke = Stopwatch.StartNew();
        var første = oppslag.Resolve("vulpes vulpes", "species", null);
        klokke.Stop();

        var etterMinne = GC.GetTotalMemory(forceFullCollection: true);

        _output.WriteLine($"Lasting + første oppslag: {klokke.ElapsedMilliseconds} ms");
        _output.WriteLine($"Minne: {(etterMinne - førMinne) / 1048576.0:F1} MB");
        _output.WriteLine(første is not null
            ? $"vulpes vulpes -> takson {første.Taxon.Id}, navn {første.MatchedScientificNameId}, "
              + $"gruppe {første.Taxon.TaxonGroupId}, "
              + $"hierarki {første.Taxon.TaxonIdHierarchy.Length} nivåer"
            : "vulpes vulpes -> ingen treff");

        første.Should().NotBeNull("rødrev skal finnes i taksonomien");
        første!.Taxon.TaxonIdHierarchy.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tar 20 000 ekte navn fra basen, normaliserer dem slik importen gjør, og slår dem
    /// opp. Treffraten skal være nær 100 % — et navn som står i taksonomien skal kunne
    /// finnes igjen. Avvik her er navn kaskaden ikke klarer å bestemme entydig.
    /// </summary>
    [Fact]
    public void Finner_igjen_navn_som_star_i_taksonomien()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var oppslag = new TaxonNameResolver(
            new DirekteKilde(connectionString), NullLogger<TaxonNameResolver>.Instance);

        List<(string Navn, string? Forfatter, string Rang)> prøver;

        using (var context = NyKontekst(connectionString))
        {
            prøver = context.Set<TaxonName>().AsNoTracking()
                .Where(x => !x.IsDeleted && x.TaxonId != null)
                .OrderBy(x => x.Id)
                .Take(20000)
                .Join(context.Set<Taxon>().AsNoTracking(), n => n.TaxonId, t => t.Id, (n, t) => new
                {
                    n.ScientificName, n.ScientificNameAuthorship, t.TaxonRankId
                })
                .Join(context.Set<TaxonRank>().AsNoTracking(), x => x.TaxonRankId, r => r.Id, (x, r) => new
                {
                    x.ScientificName, x.ScientificNameAuthorship, Rang = r.Name
                })
                .ToList()
                .Select(x => (x.ScientificName, x.ScientificNameAuthorship, (x.Rang ?? string.Empty).ToLowerInvariant()))
                .ToList();
        }

        var klokke = Stopwatch.StartNew();
        var treff = 0;
        var bom = new List<string>();

        foreach (var (navn, forfatter, rang) in prøver)
        {
            // Samme behandling navnet får på vei inn i oppslaget: HTML-formateringen
            // fjernes først, så normaliseres det. Kilden leverer aldri <i>-tagger —
            // de er noe taksonomien fra NorTaxa legger på.
            var utenTagger = System.Text.RegularExpressions.Regex.Replace(navn, "<[^>]*>", string.Empty);
            var søkenavn = TaxonomyHelper.CleanScientificName(utenTagger.ToLowerInvariant());

            if (string.IsNullOrWhiteSpace(søkenavn)) continue;

            if (oppslag.Resolve(søkenavn, rang, forfatter?.ToLowerInvariant()) is not null)
            {
                treff++;
            }
            else if (bom.Count < 10)
            {
                bom.Add($"{navn} / {forfatter} / {rang}");
            }
        }

        klokke.Stop();

        var rate = 100.0 * treff / prøver.Count;

        _output.WriteLine($"{prøver.Count} navn slått opp på {klokke.ElapsedMilliseconds} ms "
                          + $"({klokke.Elapsed.TotalMilliseconds / prøver.Count:F4} ms per oppslag)");
        _output.WriteLine($"Treff: {treff} av {prøver.Count} ({rate:F2} %)");

        if (bom.Count > 0)
        {
            _output.WriteLine("Eksempler på navn som ikke kunne bestemmes:");
            foreach (var b in bom) _output.WriteLine("  " + b);
        }

        rate.Should().BeGreaterThan(90, "navn som står i taksonomien skal kunne finnes igjen");
    }
}

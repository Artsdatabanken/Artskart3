using System.Diagnostics;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Måler organisasjonsregisteret mot ekte data: lastetid, minne og oppslagstid.
///
/// AVSLÅTT SOM STANDARD:
///
///   $env:ARTSKART_INDEX_CONNECTION = "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~OrganizationRegistrySmoke"
///
/// KREVER AT MIGRASJONEN ER KJØRT. Registeret leser <c>OrganizationAlias</c> og
/// <c>Organization.SourceDatasetId</c>, som kom med
/// <c>AddDataSourceAndOrganizationAliases</c>. Mot en base uten den feiler oppslaget
/// med en SQL-feil om manglende kolonne, ikke med en tom treffliste.
///
/// Trenger ikke Docker. Kun lesing.
/// </summary>
public class OrganizationRegistrySmokeTests
{
    private const string ConnectionVariable = "ARTSKART_INDEX_CONNECTION";

    private readonly ITestOutputHelper _output;

    public OrganizationRegistrySmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

    private static ServiceProvider NyLeverandør(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(x => x.ClearProviders());

        services.AddDbContext<ArtskartDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseNetTopologySuite();
                sql.CommandTimeout(600);
            });

            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IArtsKartDbContext>(sp => sp.GetRequiredService<ArtskartDbContext>());

        return services.BuildServiceProvider();
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

        using var leverandør = NyLeverandør(connectionString);

        var register = new EfOrganizationRegistry(
            leverandør.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EfOrganizationRegistry>.Instance);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var førMinne = GC.GetTotalMemory(forceFullCollection: true);

        var klokke = Stopwatch.StartNew();
        var første = register.PublisherByCode("o");
        klokke.Stop();

        var etterMinne = GC.GetTotalMemory(forceFullCollection: true);

        _output.WriteLine($"Lasting + første oppslag: {klokke.ElapsedMilliseconds} ms");
        _output.WriteLine($"Minne: {(etterMinne - førMinne) / 1048576.0:F1} MB");
        _output.WriteLine(første is not null
            ? $"o -> organisasjon {første.Id}, {første.Name}"
            : "o -> ingen treff");

        første.Should().NotBeNull("institusjonskoden o finnes i dagens data");
    }

    /// <summary>
    /// Slår opp hver institusjonskode og hver samlingskode som finnes i basen, og måler
    /// hva et oppslag koster. Oppslaget skjer per post, altså titalls millioner ganger
    /// per full import, så det må være i hundredels mikrosekunder — ikke millisekunder.
    /// </summary>
    [Fact]
    public void Slaar_opp_alle_koder_som_finnes()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        using var leverandør = NyLeverandør(connectionString);

        var register = new EfOrganizationRegistry(
            leverandør.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EfOrganizationRegistry>.Instance);

        using var scope = leverandør.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();

        var institusjoner = context.Set<Organization>().AsNoTracking()
            .Where(x => x.OrganizationTypeId == 1 && x.Code != null && x.Code != "")
            .Select(x => new { x.Id, Kode = x.Code! })
            .ToList();

        var samlinger = context.Set<Organization>().AsNoTracking()
            .Where(x => x.OrganizationTypeId == 2 && x.ParentId != null && x.Code != null && x.Code != "")
            .Select(x => new { x.Id, Forelder = x.ParentId!.Value, Kode = x.Code! })
            .ToList();

        // Første oppslag laster registeret. Målingen under skal ikke inkludere det.
        register.PublisherByCode(institusjoner[0].Kode);

        var klokke = Stopwatch.StartNew();

        var institusjonstreff = institusjoner.Count(x => register.PublisherByCode(x.Kode)?.Id == x.Id);
        var samlingstreff = samlinger.Count(x => register.DatasetByCode(x.Forelder, x.Kode)?.Id == x.Id);

        klokke.Stop();

        var oppslag = institusjoner.Count + samlinger.Count;

        _output.WriteLine($"Institusjoner: {institusjonstreff}/{institusjoner.Count}");
        _output.WriteLine($"Samlinger: {samlingstreff}/{samlinger.Count}");
        _output.WriteLine($"{oppslag} oppslag på {klokke.Elapsed.TotalMilliseconds:F1} ms "
                          + $"= {klokke.Elapsed.TotalMilliseconds * 1000 / oppslag:F2} µs per oppslag");

        institusjonstreff.Should().Be(institusjoner.Count, "hver institusjonskode i basen skal kunne slås opp");
        samlingstreff.Should().Be(samlinger.Count, "hver samlingskode i basen skal kunne slås opp");
    }

    /// <summary>
    /// Avstemmingen fra planen: hver institusjonskode cachen inneholder skal kunne slås
    /// opp på <c>Organization.Code</c> eller <c>OrganizationAlias.Alias</c>. Koder som
    /// ikke kan det blir avviste poster i produksjon.
    ///
    /// Krever begge tilkoblingene.
    /// </summary>
    [Fact]
    public void Avstemmer_institusjonskodene_i_cachen()
    {
        var indeks = ConnectionString;
        var cache = Environment.GetEnvironmentVariable("ARTSKART_CACHE_CONNECTION");

        if (string.IsNullOrWhiteSpace(indeks) || string.IsNullOrWhiteSpace(cache))
        {
            _output.WriteLine("ARTSKART_INDEX_CONNECTION og ARTSKART_CACHE_CONNECTION må begge være satt "
                              + "- testen gjør ingenting.");
            return;
        }

        using var leverandør = NyLeverandør(indeks);

        var register = new EfOrganizationRegistry(
            leverandør.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EfOrganizationRegistry>.Instance);

        var options = new DbContextOptionsBuilder<Artskart3.Infrastructure.Cache.ArtskartCacheDbContext>()
            .UseSqlServer(cache, sql => sql.CommandTimeout(600))
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var cacheContext = new Artskart3.Infrastructure.Cache.ArtskartCacheDbContext(options);

        var koder = cacheContext.InstitutionCodes
            .AsNoTracking()
            .Select(x => x.Code)
            .Where(x => x != null && x != "")
            .ToList();

        var uten = koder
            .Where(x => register.PublisherByCode(x!) is null && register.PublisherByAlias(x!) is null)
            .ToList();

        _output.WriteLine($"{koder.Count} institusjonskoder i cachen, {uten.Count} uten treff");

        foreach (var kode in uten)
        {
            _output.WriteLine($"  mangler: {kode}");
        }

        uten.Should().BeEmpty(
            "en institusjonskode uten treff gir avviste poster, og de forsvinner uten at noen ser det");
    }
}

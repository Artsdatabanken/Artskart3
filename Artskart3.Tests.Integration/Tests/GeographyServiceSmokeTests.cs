using System.Diagnostics;
using Artskart3.Core.Application.Import;
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
/// Måler det geografitjenestens design hviler på: hvor mye minne 4 224
/// områdegeometrier koster, og hvor fort oppslaget blir når de ligger der.
///
/// AVSLÅTT SOM STANDARD. Krever en indeksbase med områdedata:
///
///   $env:ARTSKART_INDEX_CONNECTION = "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~GeographyServiceSmoke"
///
/// Bruker ikke DatabaseFixture, og trenger derfor ikke Docker. Kun lesing.
/// </summary>
public class GeographyServiceSmokeTests
{
    private const string ConnectionVariable = "ARTSKART_INDEX_CONNECTION";

    private readonly ITestOutputHelper _output;

    public GeographyServiceSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

    /// <summary>
    /// Laster områdene rett fra basen. <c>EfAreaGeometrySource</c> bruker en
    /// scope-fabrikk fra DI; her er det enklere å gå direkte på konteksten.
    /// </summary>
    private sealed class DirekteKilde(string connectionString) : IAreaGeometrySource
    {
        public IReadOnlyList<AreaGeometry> LoadAll()
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

            using var context = new ArtskartDbContext(options);

            var rader = context.Set<Area>()
                .AsNoTracking()
                .Where(a => a.WktPolygon != null)
                .Join(
                    context.Set<AreaType>().AsNoTracking(),
                    a => a.AreaTypeId,
                    t => t.Id,
                    (a, t) => new
                    {
                        a.Id, a.Fid, a.Name, a.AreaTypeId, t.IsRequired, a.IsCurrent, a.ParentFid, a.WktPolygon
                    })
                .ToList();

            return rader
                .Select(x => new AreaGeometry(
                    x.Id, x.Fid, x.Name, x.AreaTypeId, x.IsRequired, x.IsCurrent, x.ParentFid, x.WktPolygon!))
                .ToList();
        }
    }

    [Fact]
    public void Maaler_minne_og_oppslagstid_for_omradene()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var tjeneste = new GeographyService(
            new DirekteKilde(connectionString),
            new ProjNetCoordinateTransformer(),
            NullLogger<GeographyService>.Instance);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var førMinne = GC.GetTotalMemory(forceFullCollection: true);

        var klokke = Stopwatch.StartNew();

        // Første oppslag utløser lastingen.
        var oslo = tjeneste.GetAreasForCoordinate(262000, 6643000);

        klokke.Stop();
        var lastetid = klokke.ElapsedMilliseconds;

        var etterMinne = GC.GetTotalMemory(forceFullCollection: true);
        var minneMb = (etterMinne - førMinne) / 1048576.0;

        _output.WriteLine($"Lasting + første oppslag: {lastetid} ms");
        _output.WriteLine($"Minne etter lasting: {minneMb:F1} MB (82,9 MB geometri i basen)");
        _output.WriteLine($"Oslo (262000, 6643000) traff {oslo.Count} områder: "
                          + string.Join(", ", oslo.Select(x => $"{x.AreaTypeId}/{x.Fid} {x.Name}")));

        oslo.Should().NotBeEmpty("en koordinat i Oslo skal treffe kommune og fylke");
        oslo.Should().Contain(x => x.AreaTypeId == 1, "kommune");
        oslo.Should().Contain(x => x.AreaTypeId == 2, "fylke");

        // 10 000 oppslag spredt over Norge, for å måle hva et oppslag koster når
        // områdene først ligger i minnet. Artskart 2 bruker 4,6 ms per nytt oppslag mot
        // databasen.
        var tilfeldig = new Random(42);
        klokke.Restart();
        var treff = 0;

        for (var i = 0; i < 10000; i++)
        {
            var east = tilfeldig.Next(-70000, 1100000);
            var north = tilfeldig.Next(6450000, 7950000);

            if (tjeneste.GetAreasForCoordinate(east, north).Count > 0) treff++;
        }

        klokke.Stop();

        var perOppslag = klokke.Elapsed.TotalMilliseconds / 10000;
        _output.WriteLine($"10 000 oppslag: {klokke.ElapsedMilliseconds} ms totalt, "
                          + $"{perOppslag:F4} ms per oppslag, {treff} traff Norge");

        perOppslag.Should().BeLessThan(4.6,
            "poenget med å holde områdene i minnet er å slå Artskart 2s 4,6 ms per oppslag");
    }

    [Fact]
    public void Finner_senterpunkt_for_ekte_kommunenavn()
    {
        var connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        var tjeneste = new GeographyService(
            new DirekteKilde(connectionString),
            new ProjNetCoordinateTransformer(),
            NullLogger<GeographyService>.Instance);

        string[] kommuner = ["Oslo", "Bergen", "Trondheim", "Tromsø", "Kautokeino"];

        foreach (var kommune in kommuner)
        {
            var ok = tjeneste.TryGetMunicipalityCentroid(kommune, null, out var treff);

            _output.WriteLine(ok
                ? $"{kommune,-12} -> ({treff!.Point.X}, {treff.Point.Y}) "
                  + $"± {treff.PrecisionInMeters} m, traff på {treff.MatchedOn}"
                : $"{kommune,-12} -> ingen treff");

            ok.Should().BeTrue($"{kommune} skal finnes i områdedataene");
            treff!.Point.X.Should().NotBe(0);
            treff.PrecisionInMeters.Should().BeGreaterThan(0);
        }
    }
}

extern alias workers;

using Artskart3.Infrastructure.Maintenance;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Kjorer tellingene mot testbasen og maaler hva de koster.
///
///   $env:ARTSKART_WRITE_TEST_CONNECTION = "..."
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~ObservationCountSmoke"
///
/// SKRIVER, saa den krever markortabellen som alle skrivetestene.
/// </summary>
public class ObservationCountSmokeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Oppdaterer_tellingene()
    {
        var skrivebase = Environment.GetEnvironmentVariable("ARTSKART_WRITE_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(skrivebase))
        {
            output.WriteLine("ARTSKART_WRITE_TEST_CONNECTION er ikke satt - testen gjor ingenting.");
            return;
        }

        await ObservationWriterRoundTripTests.KrevTestbase(skrivebase);

        var tjeneste = new ObservationCountService(
            new MaintenanceOptions { ConnectionString = skrivebase },
            NullLogger<ObservationCountService>.Instance);

        var resultater = await tjeneste.UpdateAllAsync(CancellationToken.None);

        foreach (var r in resultater)
        {
            output.WriteLine($"{r.Table,-16} {r.Elapsed.TotalMilliseconds,8:F0} ms  {r.Error ?? "ok"}");
        }

        resultater.Should().OnlyContain(x => x.Succeeded);
        resultater.Should().HaveCount(8);
    }
}

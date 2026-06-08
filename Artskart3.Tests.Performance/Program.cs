using Artskart3.Tests.Performance;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Artskart3 Ytelsestester");
Console.WriteLine("=======================");
Console.WriteLine();
Console.WriteLine("Tilkoblingsstrengen hentes fra (prioritert rekkefølge):");
Console.WriteLine("  1. Miljøvariabel: ARTSKART_BENCH_CONNECTION_STRING");
Console.WriteLine("  2. Bruker-secrets (nøkkel: ARTSKART_BENCH_CONNECTION_STRING)");
Console.WriteLine();
Console.WriteLine("Sett bruker-secret med:");
Console.WriteLine("  dotnet user-secrets set \"ARTSKART_BENCH_CONNECTION_STRING\" \"<tilkoblingsstreng>\"");
Console.WriteLine();

var appConfig = new ConfigurationBuilder()
    .AddUserSecrets<SearchServiceBenchmarks>()
    .AddEnvironmentVariables()
    .Build();

var influxUrl = appConfig["ARTSKART_BENCH_INFLUX_URL"] ?? "http://localhost:8086";
var influxReachable = await CheckInfluxDbAsync(influxUrl);
if (!influxReachable)
{
    Console.WriteLine($"[InfluxDB] Kan ikke na InfluxDB pa {influxUrl}");
    Console.WriteLine("[InfluxDB] Er Docker-containerne oppe? Kjor: docker compose up -d");
    return;
}
else
{
    Console.WriteLine($"[InfluxDB] Tilkoblet {influxUrl} — resultater vil bli eksportert etter kjoring.");
    Console.WriteLine();
}

var benchmarkConfig = ManualConfig.Create(DefaultConfig.Instance)
    .AddExporter(JsonExporter.FullCompressed);

BenchmarkSwitcher
    .FromTypes([typeof(SearchServiceBenchmarks), typeof(ObservationSearchBenchmarks), typeof(LookupServiceBenchmarks)])
    .Run(args, benchmarkConfig);

await BenchmarkToInfluxDb.ImportAsync(appConfig);

static async Task<bool> CheckInfluxDbAsync(string url)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        var response = await client.GetAsync($"{url}/health");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

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

var benchmarkConfig = ManualConfig.Create(DefaultConfig.Instance)
    .AddExporter(JsonExporter.FullCompressed);

BenchmarkRunner.Run<SearchServiceBenchmarks>(benchmarkConfig, args);
BenchmarkRunner.Run<LookupServiceBenchmarks>(benchmarkConfig, args);

await BenchmarkToInfluxDb.ImportAsync(appConfig);

using Artskart3.Tests.Performance;
using BenchmarkDotNet.Running;

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

BenchmarkRunner.Run<SearchServiceBenchmarks>();

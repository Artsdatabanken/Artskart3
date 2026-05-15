using Artskart3.Tests.Performance;
using BenchmarkDotNet.Running;

Console.WriteLine("Artskart3 Performance Benchmarks");
Console.WriteLine("=================================");
Console.WriteLine();
Console.WriteLine("Requires environment variable:");
Console.WriteLine("  ARTSKART_BENCH_CONNECTION_STRING=<connection string to production-like DB>");
Console.WriteLine();

BenchmarkRunner.Run<SearchServiceBenchmarks>();

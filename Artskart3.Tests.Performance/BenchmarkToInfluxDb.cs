using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Tests.Performance;

internal static class BenchmarkToInfluxDb
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task ImportAsync(IConfiguration config)
    {
        var url    = config["ARTSKART_BENCH_INFLUX_URL"]    ?? "http://localhost:8086";
        var token  = config["ARTSKART_BENCH_INFLUX_TOKEN"]  ?? "benchmark-local-token";
        var org    = config["ARTSKART_BENCH_INFLUX_ORG"]    ?? "artsdatabanken";
        var bucket = config["ARTSKART_BENCH_INFLUX_BUCKET"] ?? "benchmarks";

        var jsonFile = FindNewestReportFile();
        if (jsonFile is null)
        {
            Console.WriteLine("[InfluxDB] Ingen JSON-rapport funnet – hopper over import.");
            return;
        }

        Console.WriteLine($"[InfluxDB] Importerer: {jsonFile.Name}");

        var report = ParseReport(jsonFile);
        if (report?.Benchmarks is null or { Count: 0 })
        {
            Console.WriteLine("[InfluxDB] JSON inneholder ingen benchmarks.");
            return;
        }

        var lineProtocol = BuildLineProtocol(report.Benchmarks, jsonFile.LastWriteTimeUtc);
        await PostToInfluxAsync(url, token, org, bucket, lineProtocol);
    }

    private static FileInfo? FindNewestReportFile()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, "BenchmarkDotNet.Artifacts", "results"),
            Path.Combine(cwd, "..", "BenchmarkDotNet.Artifacts", "results"),
        };

        foreach (var dir in candidates)
        {
            var resolved = Path.GetFullPath(dir);
            if (!Directory.Exists(resolved))
                continue;

            var file = new DirectoryInfo(resolved)
                .GetFiles("*-report-full-compressed.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (file is not null)
                return file;
        }

        return null;
    }

    private static BenchmarkReport? ParseReport(FileInfo file)
    {
        using var stream = file.OpenRead();
        return JsonSerializer.Deserialize<BenchmarkReport>(stream, JsonOptions);
    }

    private static string BuildLineProtocol(List<BenchmarkEntry> benchmarks, DateTime fileTimeUtc)
    {
        var epoch   = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fileTs  = new DateTimeOffset(DateTime.SpecifyKind(fileTimeUtc, DateTimeKind.Utc), TimeSpan.Zero);
        var nanos   = (fileTs - epoch).Ticks * 100; // 1 tick = 100 nanoseconds
        var machine = EscapeTagValue(Environment.MachineName);
        var sb      = new StringBuilder();

        foreach (var b in benchmarks)
        {
            var method   = EscapeTagValue(b.FullName.Split('.').Last());
            var category = EscapeTagValue(method.Split('_')[0]);

            Console.WriteLine($"  {method,-55} mean = {b.Statistics.Mean / 1e6,8:F2} ms");

            sb.AppendLine(
                $"benchmark,category={category},method={method},machine={machine} " +
                $"mean={b.Statistics.Mean}," +
                $"median={b.Statistics.Median}," +
                $"stddev={b.Statistics.StandardDeviation}," +
                $"allocated={b.Memory.BytesAllocatedPerOperation}i " +
                $"{nanos}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string EscapeTagValue(string value) =>
        value.Replace(",", @"\,").Replace(" ", @"\ ").Replace("=", @"\=");

    private static async Task PostToInfluxAsync(string url, string token, string org, string bucket, string body)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);

        var uri     = $"{url}/api/v2/write?org={Uri.EscapeDataString(org)}&bucket={Uri.EscapeDataString(bucket)}&precision=ns";
        var content = new StringContent(body, Encoding.UTF8, "text/plain");

        try
        {
            var response = await client.PostAsync(uri, content);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"\n[InfluxDB] ✓ Importert til {url}  →  Åpne Grafana: http://localhost:4242");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n[InfluxDB] Feil fra InfluxDB ({response.StatusCode}): {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"\n[InfluxDB] Kan ikke nå InfluxDB på {url}: {ex.Message}");
            Console.WriteLine("[InfluxDB] Er Docker-containerne oppe? Kjør: docker compose up -d");
        }
    }

    // ---------------------------------------------------------------------------
    // Minimal JSON-modell for BenchmarkDotNet full-compressed rapport
    // ---------------------------------------------------------------------------

    private record BenchmarkReport(List<BenchmarkEntry> Benchmarks);
    private record BenchmarkEntry(string FullName, BenchmarkStatistics Statistics, BenchmarkMemory Memory);
    private record BenchmarkStatistics(double Mean, double Median, double StandardDeviation);
    private record BenchmarkMemory(long BytesAllocatedPerOperation);
}

using System.Globalization;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace Artskart3.Infrastructure.Services;

public sealed class ObservationMapService(IHttpClientFactory clients) : IObservationMapService, IDisposable
{
    public const string ClientName = "ObservationMaps";
    private const int MaximumResponseBytes = 12 * 1024 * 1024;
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    private readonly SemaphoreSlim renders = new(4);
    // Fixed stripes avoid an unbounded per-coordinate lock registry.
    private readonly SemaphoreSlim[] cacheLocks = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();

    public async Task<byte[]> RenderAsync(ObservationPointDto point, bool overview, CancellationToken cancellationToken)
    {
        var key = FormattableString.Invariant($"v1/{overview}/{point.East}/{point.North}/{point.Latitude}");
        if (cache.TryGetValue<byte[]>(key, out var cached)) return cached!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var gate = cacheLocks[Math.Abs(StringComparer.Ordinal.GetHashCode(key) % cacheLocks.Length)];
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (cache.TryGetValue<byte[]>(key, out cached)) return cached!;
            return await RenderAndCacheAsync(key, point, overview, timeout.Token);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<byte[]> RenderAndCacheAsync(string key, ObservationPointDto point, bool overview, CancellationToken cancellationToken)
    {
        var entered = false;
        try
        {
            await renders.WaitAsync(cancellationToken);
            entered = true;
            var width = overview ? 500 : 800;
            const int height = 800;
            var extent = GetExtent(point, overview);
            using var surface = SKSurface.Create(new SKImageInfo(width, height))
                ?? throw new InvalidDataException("Cannot allocate observation map");
            surface.Canvas.Clear(SKColors.White);
            await DrawLayerAsync(surface.Canvas, extent, width, height,
                "https://wms.geonorge.no/skwms1/wms.gebco_skyggerelieff2", "gebco_skyggerelieff2", cancellationToken);
            await DrawLayerAsync(surface.Canvas, extent, width, height,
                "https://wms.geonorge.no/skwms1/wms.terrengmodell", "relieff", cancellationToken);
            if (!overview && point.Latitude < 74)
            {
                await DrawLayerAsync(surface.Canvas, extent, width, height,
                    "https://wms.geonorge.no/skwms1/wms.norges_grunnkart", "Norges_grunnkart", cancellationToken);
            }
            if (overview || point.Latitude >= 74)
            {
                await DrawLayerAsync(surface.Canvas, extent, width, height,
                    "https://geodata.npolar.no/arcgis/services/Basisdata/NP_Basiskart_Svalbard_WMS/MapServer/WmsServer",
                    "1,2,3,4,5,6,7,8,9,10,11,13,14,15,17,18,19,20,21,22,23,25,26,27,28,29,30,31,33,34,35,36,37,38,39,40,41,42,43,44,45",
                    cancellationToken);
            }
            var x = (float)((point.East - extent[0]) / (extent[2] - extent[0]) * width);
            var y = (float)((extent[3] - point.North) / (extent[3] - extent[1]) * height);
            using var marker = new SKPaint { Color = new SKColor(0, 90, 113), IsAntialias = true };
            surface.Canvas.DrawCircle(x, y, 11, marker);
            marker.Color = SKColors.White;
            marker.Style = SKPaintStyle.Stroke;
            marker.StrokeWidth = 2;
            surface.Canvas.DrawCircle(x, y, 11, marker);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();
            cache.Set(key, bytes, new MemoryCacheEntryOptions().SetSize(bytes.Length).SetAbsoluteExpiration(TimeSpan.FromMinutes(15)));
            return bytes;
        }
        finally
        {
            if (entered) renders.Release();
        }
    }

    public static double[] GetExtent(ObservationPointDto point, bool overview)
    {
        if (!overview)
            return [point.East - 41074.6, point.North - 41074.6, point.East + 41074.6, point.North + 41074.6];
        var minX = Math.Min(-350770, point.East - 50000d);
        var minY = Math.Min(6400000, point.North - 50000d);
        var maxX = Math.Max(1100000, point.East + 50000d);
        var maxY = Math.Max(9000000, point.North + 50000d);
        var width = Math.Max(maxX - minX, (maxY - minY) * 500 / 800);
        var height = width * 800 / 500;
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        return [centerX - width / 2, centerY - height / 2, centerX + width / 2, centerY + height / 2];
    }

    private async Task DrawLayerAsync(SKCanvas canvas, double[] extent, int width, int height,
        string endpoint, string layers, CancellationToken cancellationToken)
    {
        var bbox = string.Join(",", extent.Select(n => n.ToString("R", CultureInfo.InvariantCulture)));
        var url = $"{endpoint}?SERVICE=WMS&VERSION=1.1.1&REQUEST=GetMap&SRS=EPSG:25833&BBOX={bbox}" +
            $"&WIDTH={width}&HEIGHT={height}&FORMAT=image/png&TRANSPARENT=TRUE&STYLES=&LAYERS={Uri.EscapeDataString(layers)}";
        using var client = clients.CreateClient(ClientName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumResponseBytes ||
            response.Content.Headers.ContentType?.MediaType is not ("image/png" or "image/jpeg"))
            throw new InvalidDataException("Invalid WMS image response");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumResponseBytes) throw new InvalidDataException("WMS image exceeds size limit");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        using var data = SKData.CreateCopy(buffer.ToArray());
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("WMS did not return a decodable image");
        if (codec.Info.Width != width || codec.Info.Height != height) throw new InvalidDataException("Unexpected WMS image dimensions");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("WMS image decoding failed");
        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
    }

    public void Dispose()
    {
        cache.Dispose();
        renders.Dispose();
        foreach (var gate in cacheLocks) gate.Dispose();
    }
}

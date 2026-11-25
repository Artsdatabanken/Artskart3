using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace Artskart3.Infrastructure.Services;

public sealed class ObservationMapService(IHttpClientFactory clients) : IObservationMapService, IDisposable
{
    public const string ClientName = "ObservationMaps";
    public const string ContentType = "image/webp";
    public const int LocalSize = 800;
    public const int OverviewWidth = 500;
    public const int OverviewHeight = 800;

    // Økes når tegningen endres, så verken minnebufferen eller nettleserne viser gamle bilder.
    private const int RenderVersion = 3;
    // Halve sidelengden i nærkartet når verken presisjonsringen eller formen trenger mer plass.
    private const double LocalHalfSize = 2_500;
    // Lenger ut zoomes ikke nærkartet, selv om ringen eller formen er større.
    private const double MaximumHalfSize = 250_000;
    private const float MarkerRadius = 11;
    private const int MaximumResponseBytes = 12 * 1024 * 1024;
    private const int WebpQuality = 80;
    // Luft rundt ringen og formen, så kanten deres synes.
    private const double FitMargin = 1.2;
    private const float OutlineWidth = 3;
    private const float MaskingSquareOutlineWidth = 4;
    private static readonly float[] MaskingSquareDash = [20, 12];
    private static readonly SKColor MarkerColor = new(0, 90, 113);
    private static readonly SKColor AreaFill = MarkerColor.WithAlpha(38);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan MapLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BackgroundLifetime = TimeSpan.FromDays(1);
    private const string OverviewBackgroundKey = "oversiktsbakgrunn";

    // Standardutsnittet i oversiktskartet, og hvor mye luft funnet skal ha når det ligger utenfor.
    private const double OverviewMinX = -350770, OverviewMinY = 6400000, OverviewMaxX = 1100000, OverviewMaxY = 9000000;
    private const double OverviewMargin = 50000;
    private static readonly double[] DefaultOverviewExtent = FitOverview(OverviewMinX, OverviewMinY, OverviewMaxX, OverviewMaxY);

    private static readonly WmsLayer Gebco = new("https://wms.geonorge.no/skwms1/wms.gebco_skyggerelieff2", "gebco_skyggerelieff2");
    private static readonly WmsLayer Terrain = new("https://wms.geonorge.no/skwms1/wms.terrengmodell", "relieff");
    private static readonly WmsLayer BaseMap = new("https://wms.geonorge.no/skwms1/wms.norges_grunnkart", "Norges_grunnkart");
    private static readonly WmsLayer Svalbard = new(
        "https://geodata.npolar.no/arcgis/services/Basisdata/NP_Basiskart_Svalbard_WMS/MapServer/WmsServer",
        "1,2,3,4,5,6,7,8,9,10,11,13,14,15,17,18,19,20,21,22,23,25,26,27,28,29,30,31,33,34,35,36,37,38,39,40,41,42,43,44,45");
    private static readonly WmsLayer[] MainlandLayers = [Gebco, Terrain, BaseMap];
    private static readonly WmsLayer[] SvalbardLayers = [Gebco, Terrain, Svalbard];

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });

    // Ett felles tak for alle brukere på tegninger; bilder som allerede ligger i bufferen, går
    // utenom. Plassen tas før stripelåsen, så den som holder en stripe alltid kan tegne ferdig,
    // og en full kø gir 429 med en gang i stedet for venting.
    private readonly ConcurrencyLimiter renders = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = 4,
        QueueLimit = 16,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    });

    // Faste striper i stedet for én lås per bilde, som ville vokst uten grense.
    private readonly SemaphoreSlim[] cacheLocks = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();

    // Egen lås for den delte oversiktsbakgrunnen, som hentes mens oversiktsbildet holder sin stripe.
    private readonly SemaphoreSlim backgroundLock = new(1);

    public string Fingerprint(ObservationMapSubject subject, bool overview)
    {
        var point = RequirePoint(subject);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"{RenderVersion}/{overview}/{point.East}/{point.North}/{point.Latitude:R}")));
        if (!overview)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(FormattableString.Invariant(
                $"/{subject.CoordinatePrecision}/{subject.ShapeIsMaskingSquare}/")));
            if (subject.Shape is { } shape) hash.AppendData(shape.AsBinary());
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public async Task<ObservationImageFile> RenderAsync(ObservationMapSubject subject, bool overview, CancellationToken cancellationToken)
    {
        var point = RequirePoint(subject);
        var key = Fingerprint(subject, overview);
        if (cache.TryGetValue<byte[]>(key, out var cached)) return new ObservationImageFile(cached!, ContentType);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RenderTimeout);
        using var lease = await renders.AcquireAsync(1, timeout.Token);
        if (!lease.IsAcquired) throw new ObservationMapBusyException();
        var gate = cacheLocks[Math.Abs(StringComparer.Ordinal.GetHashCode(key) % cacheLocks.Length)];
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (!cache.TryGetValue(key, out cached))
            {
                cached = overview
                    ? await RenderOverviewAsync(point, timeout.Token)
                    : await RenderLocalAsync(subject, point, timeout.Token);
                Store(key, cached, MapLifetime, CacheItemPriority.Normal);
            }
            return new ObservationImageFile(cached!, ContentType);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Utsnittet i nærkartet, og om formen eller en presisjonsring tegnes.</summary>
    public static LocalMapLayout GetLocalLayout(ObservationMapSubject subject)
    {
        var point = RequirePoint(subject);
        if (subject.Shape is { } shape)
        {
            var envelope = shape.EnvelopeInternal;
            var size = Math.Max(envelope.Width, envelope.Height);
            var halfSize = Math.Clamp(size / 2 * FitMargin, LocalHalfSize, MaximumHalfSize);
            // En form som blir mindre enn markøren, vises som punktet.
            if (size * LocalSize / (2 * halfSize) >= 2 * MarkerRadius)
                return new(Square(envelope.Centre.X, envelope.Centre.Y, halfSize), true, 0);
        }
        else if (subject.CoordinatePrecision is { } precision)
        {
            var halfSize = Math.Clamp(precision * FitMargin, LocalHalfSize, MaximumHalfSize);
            var radius = (float)(precision * LocalSize / (2 * halfSize));
            // En ring som ikke når ut forbi markøren, viser ingenting.
            if (radius > MarkerRadius + OutlineWidth)
                return new(Square(point.East, point.North, halfSize), false, radius);
        }
        return new(Square(point.East, point.North, LocalHalfSize), false, 0);
    }

    public static double[] GetOverviewExtent(ObservationPointDto point) => FitOverview(
        Math.Min(OverviewMinX, point.East - OverviewMargin), Math.Min(OverviewMinY, point.North - OverviewMargin),
        Math.Max(OverviewMaxX, point.East + OverviewMargin), Math.Max(OverviewMaxY, point.North + OverviewMargin));

    private static double[] FitOverview(double minX, double minY, double maxX, double maxY)
    {
        var width = Math.Max(maxX - minX, (maxY - minY) * OverviewWidth / OverviewHeight);
        var height = width * OverviewHeight / OverviewWidth;
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        return [centerX - width / 2, centerY - height / 2, centerX + width / 2, centerY + height / 2];
    }

    private void Store(string key, byte[] bytes, TimeSpan lifetime, CacheItemPriority priority) =>
        cache.Set(key, bytes, new MemoryCacheEntryOptions()
            .SetSize(bytes.Length).SetAbsoluteExpiration(lifetime).SetPriority(priority));

    private async Task<byte[]> RenderLocalAsync(ObservationMapSubject subject, ObservationPointDto point, CancellationToken cancellationToken)
    {
        var layout = GetLocalLayout(subject);
        var map = new MapProjection(layout.Extent, LocalSize, LocalSize);
        using var surface = CreateSurface(LocalSize, LocalSize);
        await DrawLayersAsync(surface.Canvas, map, point.Latitude < 74 ? MainlandLayers : SvalbardLayers, cancellationToken);
        if (layout.ShowShape)
        {
            DrawShape(surface.Canvas, subject.Shape!, map, subject.ShapeIsMaskingSquare);
        }
        else
        {
            var center = map.ToPixel(point.East, point.North);
            if (layout.RingRadius > 0) DrawRing(surface.Canvas, center, layout.RingRadius);
            DrawMarker(surface.Canvas, center);
        }
        return Encode(surface, SKEncodedImageFormat.Webp, WebpQuality);
    }

    private async Task<byte[]> RenderOverviewAsync(ObservationPointDto point, CancellationToken cancellationToken)
    {
        var map = new MapProjection(GetOverviewExtent(point), OverviewWidth, OverviewHeight);
        using var surface = CreateSurface(OverviewWidth, OverviewHeight);
        if (map.Extent.SequenceEqual(DefaultOverviewExtent))
        {
            var background = await GetOverviewBackgroundAsync(map, cancellationToken);
            using var image = SKImage.FromEncodedData(background) ?? throw new InvalidDataException("Overview background is not decodable");
            surface.Canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        }
        else
        {
            // Funn ute ved kanten får sitt eget utsnitt, så bakgrunnen deres kan ikke deles.
            await DrawLayersAsync(surface.Canvas, map, SvalbardLayers, cancellationToken);
        }
        DrawMarker(surface.Canvas, map.ToPixel(point.East, point.North));
        return Encode(surface, SKEncodedImageFormat.Webp, WebpQuality);
    }

    /// <summary>Bakgrunnen er lik for nesten alle funn, så den hentes én gang og bare markøren tegnes per funn.</summary>
    private async Task<byte[]> GetOverviewBackgroundAsync(MapProjection map, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<byte[]>(OverviewBackgroundKey, out var cached)) return cached!;
        await backgroundLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(OverviewBackgroundKey, out cached)) return cached!;
            using var surface = CreateSurface(OverviewWidth, OverviewHeight);
            await DrawLayersAsync(surface.Canvas, map, SvalbardLayers, cancellationToken);
            var bytes = Encode(surface, SKEncodedImageFormat.Png, 100);
            Store(OverviewBackgroundKey, bytes, BackgroundLifetime, CacheItemPriority.High);
            return bytes;
        }
        finally
        {
            backgroundLock.Release();
        }
    }

    /// <summary>Henter lagene samtidig og tegner dem i rekkefølge. Feiler ett lag, avbrytes de andre.</summary>
    private async Task DrawLayersAsync(SKCanvas canvas, MapProjection map, WmsLayer[] layers, CancellationToken cancellationToken)
    {
        using var firstFailure = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var images = await Task.WhenAll(layers.Select(async layer =>
        {
            try
            {
                return await FetchLayerAsync(layer, map, firstFailure.Token);
            }
            catch
            {
                await firstFailure.CancelAsync();
                throw;
            }
        }));
        foreach (var bytes in images)
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("WMS did not return a decodable image");
            if (codec.Info.Width != map.Width || codec.Info.Height != map.Height)
                throw new InvalidDataException("Unexpected WMS image dimensions");
            using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("WMS image decoding failed");
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }
    }

    private async Task<byte[]> FetchLayerAsync(WmsLayer layer, MapProjection map, CancellationToken cancellationToken)
    {
        var bbox = string.Join(",", map.Extent.Select(n => n.ToString("R", CultureInfo.InvariantCulture)));
        var url = $"{layer.Endpoint}?SERVICE=WMS&VERSION=1.1.1&REQUEST=GetMap&SRS=EPSG:25833&BBOX={bbox}" +
            $"&WIDTH={map.Width}&HEIGHT={map.Height}&FORMAT=image/png&TRANSPARENT=TRUE&STYLES=&LAYERS={Uri.EscapeDataString(layer.Layers)}";
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
        return buffer.ToArray();
    }

    private static void DrawShape(SKCanvas canvas, Geometry shape, MapProjection map, bool maskingSquare)
    {
        using var fill = new SKPaint { Color = AreaFill, IsAntialias = true };
        using var dash = maskingSquare ? SKPathEffect.CreateDash(MaskingSquareDash, 0) : null;
        using var outline = new SKPaint
        {
            Color = MarkerColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = maskingSquare ? MaskingSquareOutlineWidth : OutlineWidth,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = maskingSquare ? SKStrokeCap.Butt : SKStrokeCap.Round,
            PathEffect = dash,
        };
        for (var i = 0; i < shape.NumGeometries; i++)
        {
            using var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
            var part = shape.GetGeometryN(i);
            if (part is Polygon polygon)
            {
                builder.AddPoly(map.ToPixels(polygon.ExteriorRing), true);
                foreach (var hole in polygon.InteriorRings) builder.AddPoly(map.ToPixels(hole), true);
            }
            else if (part is LineString line)
            {
                builder.AddPoly(map.ToPixels(line), false);
            }
            using var path = builder.Detach();
            if (part is Polygon) canvas.DrawPath(path, fill);
            canvas.DrawPath(path, outline);
        }
    }

    private static void DrawRing(SKCanvas canvas, SKPoint center, float radius)
    {
        using var paint = new SKPaint { Color = AreaFill, IsAntialias = true };
        canvas.DrawCircle(center, radius, paint);
        paint.Color = MarkerColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = OutlineWidth;
        canvas.DrawCircle(center, radius, paint);
    }

    private static void DrawMarker(SKCanvas canvas, SKPoint center)
    {
        using var marker = new SKPaint { Color = MarkerColor, IsAntialias = true };
        canvas.DrawCircle(center, MarkerRadius, marker);
        marker.Color = SKColors.White;
        marker.Style = SKPaintStyle.Stroke;
        marker.StrokeWidth = 2;
        canvas.DrawCircle(center, MarkerRadius, marker);
    }

    private static SKSurface CreateSurface(int width, int height)
    {
        var surface = SKSurface.Create(new SKImageInfo(width, height))
            ?? throw new InvalidDataException("Cannot allocate observation map");
        surface.Canvas.Clear(SKColors.White);
        return surface;
    }

    private static byte[] Encode(SKSurface surface, SKEncodedImageFormat format, int quality)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(format, quality) ?? throw new InvalidDataException("Cannot encode observation map");
        return data.ToArray();
    }

    private static double[] Square(double centerX, double centerY, double halfSize) =>
        [centerX - halfSize, centerY - halfSize, centerX + halfSize, centerY + halfSize];

    private static ObservationPointDto RequirePoint(ObservationMapSubject subject) =>
        subject.Point ?? throw new ArgumentException("Observation maps need a public point.", nameof(subject));

    public void Dispose()
    {
        cache.Dispose();
        renders.Dispose();
        backgroundLock.Dispose();
        foreach (var gate in cacheLocks) gate.Dispose();
    }

    private sealed record WmsLayer(string Endpoint, string Layers);

    /// <summary>Fra UTM 33-koordinater til piksler i et bilde av utsnittet.</summary>
    private readonly record struct MapProjection(double[] Extent, int Width, int Height)
    {
        public SKPoint ToPixel(double east, double north) => new(
            (float)((east - Extent[0]) / (Extent[2] - Extent[0]) * Width),
            (float)((Extent[3] - north) / (Extent[3] - Extent[1]) * Height));

        public SKPoint[] ToPixels(LineString line)
        {
            var coordinates = line.Coordinates;
            var pixels = new SKPoint[coordinates.Length];
            for (var i = 0; i < coordinates.Length; i++) pixels[i] = ToPixel(coordinates[i].X, coordinates[i].Y);
            return pixels;
        }
    }
}

/// <param name="Extent">Utsnittet i UTM 33: minX, minY, maxX, maxY.</param>
/// <param name="ShowShape">Lokasjonens form tegnes i stedet for punktet.</param>
/// <param name="RingRadius">Presisjonsringens radius i piksler, 0 når den ikke tegnes.</param>
public sealed record LocalMapLayout(double[] Extent, bool ShowShape, float RingRadius);

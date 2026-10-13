using System.IO.Compression;
using System.Text;
using Artskart3.Infrastructure.Cache;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class CacheBlobCompressionTests
{
    /// <summary>
    /// Pakker som Artskart 2 gjør det: UTF-16-bytes gjennom gzip.
    /// </summary>
    private static byte[] PakkSomArtskart2(string tekst)
    {
        var bytes = Encoding.Unicode.GetBytes(tekst);
        using var ut = new MemoryStream();
        using (var gzip = new GZipStream(ut, CompressionMode.Compress))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return ut.ToArray();
    }

    private static byte[] PakkMedUtf8(string tekst)
    {
        var bytes = Encoding.UTF8.GetBytes(tekst);
        using var ut = new MemoryStream();
        using (var gzip = new GZipStream(ut, CompressionMode.Compress))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return ut.ToArray();
    }

    [Fact]
    public void Decompress_gir_tilbake_original_tekst()
    {
        const string json = "{\"ScientificName\":\"Vulpes vulpes\",\"Locality\":\"Nordmarka\"}";

        CacheBlobCompression.Decompress(PakkSomArtskart2(json)).Should().Be(json);
    }

    [Fact]
    public void Decompress_bevarer_norske_tegn()
    {
        const string json = "{\"Locality\":\"Nærøy, Ålesund, Østfold\",\"NorskNavn\":\"rødrev\"}";

        CacheBlobCompression.Decompress(PakkSomArtskart2(json)).Should().Be(json);
    }

    [Fact]
    public void Decompress_fjerner_byte_order_mark()
    {
        const string json = "{\"Id\":\"1\"}";

        CacheBlobCompression.Decompress(PakkSomArtskart2('\uFEFF' + json)).Should().Be(json);
    }

    /// <summary>
    /// Vakten mot den fellen som koster mest tid: en «opprydding» til UTF-8 gir ingen
    /// feilmelding, bare ugyldig JSON for hver eneste post.
    /// </summary>
    [Fact]
    public void Decompress_av_utf8_blob_gir_ikke_original_tekst()
    {
        const string json = "{\"ScientificName\":\"Vulpes vulpes\"}";

        CacheBlobCompression.Decompress(PakkMedUtf8(json)).Should().NotBe(json);
    }

    [Fact]
    public void Decompress_av_noe_som_ikke_er_gzip_kaster()
    {
        var søppel = new byte[] { 1, 2, 3, 4, 5 };

        var act = () => CacheBlobCompression.Decompress(søppel);

        act.Should().Throw<InvalidDataException>();
    }
}

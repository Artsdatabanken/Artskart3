using System.IO.Compression;
using System.Text;

namespace Artskart3.Infrastructure.Cache;

/// <summary>
/// Pakker ut <c>DarwinCoreProxies.SerializedObject</c>. Port av Artskart 2s
/// <c>Sql2016CompressionHelper</c>.
///
/// UTF-16, IKKE UTF-8. Dette er hele poenget med klassen. Artskart 2 koder JSON-en
/// som <c>Encoding.Unicode</c> før gzip, fordi formatet skal være utvekslbart med SQL
/// Servers egne <c>COMPRESS</c>/<c>DECOMPRESS</c>, som arbeider på nvarchar-bytes.
/// En port som antar UTF-8 får ikke en feilmelding — den får ugyldig JSON, og hver
/// post ser ødelagt ut.
///
/// BOM: <c>Encoding.Unicode.GetString</c> fjerner ikke et eventuelt byte order mark.
/// Artskart 2 skriver ikke BOM (<c>GetBytes</c> gjør det ikke), men blobene er skrevet
/// over mange år, så vi tar høyde for at et er med.
/// </summary>
public static class CacheBlobCompression
{
    public static string Decompress(byte[] compressed)
    {
        ArgumentNullException.ThrowIfNull(compressed);

        using var kilde = new MemoryStream(compressed, writable: false);
        using var gzip = new GZipStream(kilde, CompressionMode.Decompress);
        using var utpakket = new MemoryStream();

        gzip.CopyTo(utpakket);

        var tekst = Encoding.Unicode.GetString(utpakket.GetBuffer(), 0, (int)utpakket.Length);

        return tekst.Length > 0 && tekst[0] == '\uFEFF' ? tekst[1..] : tekst;
    }
}

using System.Collections.Concurrent;
using Artskart3.Core.Application.Import;
using ProjNet.CoordinateSystems;
using ProjNet.IO.CoordinateSystems;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Koordinatsystemene importen kan transformere mellom, definert med WKT.
///
/// WKT-STRENGENE ER KOPIERT ORDRETT FRA ARTSKART 2
/// (<c>ServiceImplmentation/CoordinateTransform/CoordinateSystemRegister.cs</c>) og
/// skal ikke «ryddes». Det er de som bestemmer tallene: særlig
/// <c>TOWGS84[-87,-98,-121,0,0,0,0]</c> på ED50, som er datumskiftet alle
/// ED50-koordinater i basen er transformert med. Hentes en annen variant av samme
/// EPSG-kode fra et register, flytter gamle poster seg.
///
/// ETRS89 har <c>TOWGS84[0,0,0,0,0,0,0]</c>. ETRS89 og WGS 84 behandles altså som
/// samme datum — det er grunnen til at ommerkingen i <c>RelabelGeometrySridTo25833</c>
/// kan la koordinatene stå. Mellom 32633 og 25833 skiller bare ellipsoiden seg
/// (WGS 84 mot GRS 1980), og forskjellen i flattrykning gir millimetre i projiserte
/// meter.
///
/// BARE DET IMPORTEN BRUKER. Google Mercator og WGS 84 / UTM for andre soner enn 33
/// finnes i Artskart 2s register, men ingen importsti rører dem. 32633 er med fordi
/// det er systemet alle eksisterende rader påstår at de er i.
/// </summary>
public static class CoordinateSystemRegister
{
    private static readonly ConcurrentDictionary<int, CoordinateSystem> Cache = new();

    private static readonly Dictionary<int, string> Definitions = new()
    {
        [4326] =
            "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]]",

        [4230] =
            "GEOGCS[\"ED50\",DATUM[\"European_Datum_1950\",SPHEROID[\"International 1924\",6378388,297,AUTHORITY[\"EPSG\",\"7022\"]],TOWGS84[-87,-98,-121,0,0,0,0],AUTHORITY[\"EPSG\",\"6230\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4230\"]]",

        [4258] =
            "GEOGCS[\"ETRS89\",DATUM[\"European_Terrestrial_Reference_System_1989\",SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]],TOWGS84[0,0,0,0,0,0,0],AUTHORITY[\"EPSG\",\"6258\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4258\"]]",

        [32633] =
            "PROJCS[\"WGS 84 / UTM zone 33N\",GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",15],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AUTHORITY[\"EPSG\",\"32633\"]]",

        [25830] = Etrs89Utm(30, -3),
        [25831] = Etrs89Utm(31, 3),
        [25832] = Etrs89Utm(32, 9),
        [25833] = Etrs89Utm(33, 15),
        [25834] = Etrs89Utm(34, 21),
        [25835] = Etrs89Utm(35, 27),
        [25836] = Etrs89Utm(36, 33),
        [25837] = Etrs89Utm(37, 39)
    };

    /// <summary>
    /// De åtte ETRS89 / UTM-definisjonene er identiske bortsett fra sonenummer og
    /// midtmeridian, så de bygges av samme mal. Malen er tegn for tegn den samme som
    /// de åtte utskrevne definisjonene i Artskart 2.
    /// </summary>
    private static string Etrs89Utm(int zone, int centralMeridian) =>
        $"PROJCS[\"ETRS89 / UTM zone {zone}N\",GEOGCS[\"ETRS89\",DATUM[\"European_Terrestrial_Reference_System_1989\","
        + "SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]],AUTHORITY[\"EPSG\",\"6258\"]],"
        + "PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],"
        + "UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4258\"]],"
        + "PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],"
        + $"PARAMETER[\"central_meridian\",{centralMeridian}],PARAMETER[\"scale_factor\",0.9996],"
        + "PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],"
        + $"UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AUTHORITY[\"EPSG\",\"{25800 + zone}\"]]";

    /// <summary>
    /// Koordinatsystemet for en EPSG-kode, eller <c>null</c> hvis koden ikke er med.
    /// Parsingen gjøres én gang per kode.
    /// </summary>
    public static CoordinateSystem? Get(int epsg)
    {
        if (Cache.TryGetValue(epsg, out var cached)) return cached;

        if (!Definitions.TryGetValue(epsg, out var wkt)) return null;

        var system = (CoordinateSystem)CoordinateSystemWktReader.Parse(wkt);

        return Cache.GetOrAdd(epsg, system);
    }

    /// <summary>EPSG-kodene registeret kjenner. Rekkefølgen er ikke garantert.</summary>
    public static IReadOnlyCollection<int> SupportedEpsgCodes => Definitions.Keys;

    /// <summary>
    /// WKT-definisjonen bak en EPSG-kode. Finnes for å kunne sammenlignes med
    /// Artskart 2s strenger i en test — det er definisjonen som bestemmer tallene.
    /// </summary>
    public static string? Wkt(int epsg) => Definitions.GetValueOrDefault(epsg);

    /// <summary>
    /// Kodene importen faktisk skriver til index, tatt med her som en påminnelse om at
    /// <see cref="CoordinateSystems.Etrs89UtmZone33N"/> er den ene lagringsmerkingen.
    /// </summary>
    public static bool IsStorageSystem(int epsg) => epsg == CoordinateSystems.Etrs89UtmZone33N;
}

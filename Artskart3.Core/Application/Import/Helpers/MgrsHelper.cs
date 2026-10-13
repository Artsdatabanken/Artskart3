using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Helpers;

/// <summary>
/// Tolker MGRS-rutereferanser til UTM. Port av retningen MGRS → UTM i Artskart 2s
/// <c>MgrsHelper</c>, som er den importen bruker: mangler både lat/lon og UTM, er
/// <c>MGRSNotation</c> nest siste utvei før kommunesentroiden.
///
/// MOTSATT RETNING ER IKKE PORTERT. <c>UtmToMgrs</c>, <c>LlToMgrs</c> og
/// sonegrensematematikken rundt dem (<c>GetNorthernLimitForZonebasedOnLatitudeLongitude</c>
/// med tabellen, <c>MGRSletters</c>, <c>RawUTMToMGRS</c>) hører til visning og
/// eksport, ikke til importen. Trengs de senere, ligger de i
/// <c>Artskart.Model/Utils/MgrsHelper.cs</c>.
///
/// ETRS89, IKKE WGS 84. Artskart 2 merker resultatet med
/// <c>GetCoordinatesystemEpsgFromUtmZoneZdlEuref89</c>, altså 258xx. Rutenettet er
/// det samme i praksis — forskjellen mellom datumene er rundt én meter, og en
/// MGRS-rute er aldri mer presis enn én meter.
/// </summary>
public static class MgrsHelper
{
    private const double Gridsize = 100000.0;

    /// <summary>
    /// Sonebåndbokstavene fra 80°S til 84°N, åtte grader hver. X er med to ganger
    /// fordi det båndet er tolv grader høyt.
    /// </summary>
    private const string UtmZoneDelimiterChars = "CDEFGHJKLMNPQRSTUVWXX";

    /// <summary>MGRS-alfabetet: I og O er utelatt for å ikke forveksles med 1 og 0.</summary>
    private const string MgrsChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private static readonly Regex GyldigNotasjon = new(
        "^\\d{2}[ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjklmnpqrstuvwxyz]{3}(\\d{2}|\\d{4}|\\d{6}|\\d{8}|\\d{10})$",
        RegexOptions.Compiled);

    /// <summary>
    /// To sifre sone, tre bokstaver (sonebånd + to rutebokstaver), og deretter et likt
    /// antall sifre for easting og northing. Alt annet avvises av importen med
    /// <c>InvalidMgrsNotation</c>.
    /// </summary>
    public static bool IsValidMgrsNotation(string? mgrsNotation) =>
        !string.IsNullOrEmpty(mgrsNotation) && GyldigNotasjon.IsMatch(mgrsNotation);

    /// <summary>
    /// Tolker referansen. Gir <c>null</c> for en ugyldig sone eller en rutebokstav som
    /// ikke finnes i MGRS-alfabetet — kall <see cref="IsValidMgrsNotation"/> først.
    ///
    /// Northingen er den vanskelige delen: MGRS oppgir bare posisjonen innenfor et
    /// 2 000 km-bånd, så den absolutte northingen finnes ved å velge det båndet som
    /// ligger nærmest midtbreddegraden til sonebåndbokstaven.
    /// </summary>
    public static MgrsCoordinate? MgrsToUtm(string mgrsNotation)
    {
        ArgumentException.ThrowIfNullOrEmpty(mgrsNotation);

        var beskrivelse = MgrsDescription.Parse(mgrsNotation);
        if (beskrivelse is null) return null;

        var m = beskrivelse.Value;

        if (m.Zone is < 1 or > 60) return null;

        // Utenfor sone 30-37 har vi ingen definisjon å merke punktet med. Artskart 2
        // kaster ArgumentOutOfRangeException her, og et unntak feller hele nodeimporten
        // (plan-felle 9). En referanse i en sone vi ikke dekker er geografisk utenfor
        // Norge uansett, så den blir én avvist post i stedet.
        if (m.Zone is < 30 or > 37) return null;

        var sg1 = MgrsChars.IndexOf(m.C1);
        var sg2 = MgrsChars.IndexOf(m.C2);
        if (sg1 < 0 || sg2 < 0) return null;

        var e = 1 + (sg1 % 8);
        var z = m.Zone - 1;

        // Omtrentlig northing for midten av sonebåndet, i enheter av 100 km.
        var approxN = ZdlMedianLat(m.Zdl) * 100.0 / 90.0;

        var offset = (sg2 + 20 - (z % 2 * 5)) % 20;
        var n0 = (approxN - offset) / 20.0;
        var n1 = (int)Math.Round(n0);

        // Bokstavparet gjentar seg hvert 20 × 100 km, så vi legger til det antallet
        // 2 000 km-båndene som bringer oss nærmest approxN.
        var n = offset + (n1 * 20);

        var east = CreateCoord(e, m.Er, m.Digits);
        var north = CreateCoord(n, m.Nr, m.Digits);

        return new MgrsCoordinate(
            new GeoPoint(east, north, CoordinateSystems.Etrs89UtmFromZone(m.Zone)),
            PrecisionFromDigits(m.Digits));
    }

    /// <summary>
    /// Rutestørrelsen referansen angir, i meter: 100 km delt på ti per siffer.
    /// </summary>
    public static double? TileSize(string mgrsNotation)
    {
        ArgumentException.ThrowIfNullOrEmpty(mgrsNotation);

        var beskrivelse = MgrsDescription.Parse(mgrsNotation);

        return beskrivelse is null ? null : TileSize(beskrivelse.Value.Digits);
    }

    /// <summary>
    /// Presisjonen Artskart 2 setter per sifferlengde. Verdiene er med vilje
    /// 7-tallsrekken (1, 7, 77, 777, 7777) og ikke rutestørrelsen: de er et
    /// gjenkjennelig spor av at presisjonen kom fra en MGRS-rute og ikke fra kilden.
    /// Beholdt som de er — de ligger i 61 millioner rader.
    /// </summary>
    private static int? PrecisionFromDigits(int digits) => digits switch
    {
        5 => 1,
        4 => 7,
        3 => 77,
        2 => 777,
        1 => 7777,
        _ => null
    };

    private static double TileSize(int digits) => Gridsize / Math.Pow(10, digits);

    private static double CreateCoord(int grid, int gridSub, int digits) =>
        (grid * Gridsize) + (gridSub * TileSize(digits));

    /// <summary>
    /// Midtbreddegraden til et sonebånd. X-båndet er tolv grader høyt (72–84), så
    /// midten der er 78 og ikke 76.
    /// </summary>
    private static double ZdlMedianLat(char zdl)
    {
        var i = (double)UtmZoneDelimiterChars.IndexOf(zdl);
        if (i < 0.0) return -1.0;

        var mlat = -76.0 + (i * 8.0);

        return mlat == 76.0 ? 78.0 : mlat;
    }

    /// <summary>
    /// De oppdelte feltene i en MGRS-referanse. Port av Artskart 2s <c>MgrsDesc</c>,
    /// men med <c>Parse</c> som gir <c>null</c> i stedet for å kaste: en ugyldig
    /// referanse er én avvist post, ikke en avbrutt node.
    /// </summary>
    private readonly record struct MgrsDescription(int Zone, char Zdl, char C1, char C2, int Er, int Nr, int Digits)
    {
        public static MgrsDescription? Parse(string mgrs)
        {
            if (mgrs.Length < 7 || (mgrs.Length - 5) % 2 != 0) return null;

            var digits = (mgrs.Length - 5) / 2;

            if (!int.TryParse(mgrs.AsSpan(0, 2), out var zone)) return null;
            if (!int.TryParse(mgrs.AsSpan(5, digits), out var er)) return null;
            if (!int.TryParse(mgrs.AsSpan(5 + digits, digits), out var nr)) return null;

            return new MgrsDescription(
                zone,
                char.ToUpperInvariant(mgrs[2]),
                char.ToUpperInvariant(mgrs[3]),
                char.ToUpperInvariant(mgrs[4]),
                er,
                nr,
                digits);
        }
    }
}

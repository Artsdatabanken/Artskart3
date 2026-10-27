using System.Collections.Concurrent;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems.Transformations;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Koordinattransformasjon med ProjNET, samme bibliotekfamilie Artskart 2 bruker.
/// Definisjonene ligger i <see cref="CoordinateSystemRegister"/>.
///
/// VERSJONSFORSKJELLEN ER MÅLT. Artskart 2 kjører ProjNet 1.2 (med GeoAPI), her
/// kjører ProjNET 2.1. Samme WKT gjennom begge bibliotekene, Oslo (10,75 / 59,91) og
/// Tromsø (18,9553 / 69,6489):
///
/// <list type="bullet">
/// <item>WGS 84 → 25833: avvik 0,02 mm øst og 0,8 mm nord</item>
/// <item>ED50 → 25833: samme størrelsesorden, 0,02 mm og 0,8 mm</item>
/// <item>25833 → WGS 84: 7e-8 grader, altså under en centimeter</item>
/// <item>ED50 → WGS 84: identisk til niende desimal</item>
/// </list>
///
/// MEN MÅLINGEN OVER ER NÆR MIDTMERIDIANEN, OG DEN ER IKKE HELE BILDET. Avviket er
/// null på 15°Ø og vokser med avstanden derfra, fordi de to versjonenes rekkeutvikling
/// for Transverse Mercator divergerer utenfor sonen. Målt 14. oktober 2026, avvik i
/// østverdi: 0,001 m ved 21°Ø, 0,08 m ved 27°Ø og 0,4 m ved 30°Ø. Nordverdiene er
/// upåvirket.
///
/// DET BETYR AT ØST-FINNMARK FÅR ANDRE TALL ENN ARTSKART 2 SKREV. Målt på node 43 fikk
/// 93 av 8 658 poster — alle rundt 29,3°Ø — én meter større <c>East</c>, fordi avviket
/// på 0,26 m krysset avrundingsgrensen. Lengde- og breddegrad var bit-identiske.
///
/// VI BEHOLDER 2.1. Avviket er null på midtmeridianen og vokser med avstanden, som er
/// signaturen til en avkortet rekke i 1.2 — så 2.1 er den riktigere av de to, og
/// Artskart 3 skriver bedre koordinater enn Artskart 2 gjorde. Konsekvensen er at
/// paritetsriggen ikke kan kreve bit-identiske koordinater øst for ~26°Ø. Se
/// CacheToIndexPlan 7.2 punkt 11 — beslutningen om å akseptere endringen i publiserte
/// tall ligger hos ledelsen, ikke her.
///
/// Transformasjonene er tråd-trygge etter opprettelse og bufres per (kilde, mål)-par,
/// slik Artskart 2 også gjør det.
/// </summary>
public class ProjNetCoordinateTransformer : ICoordinateTransformer
{
    private static readonly CoordinateTransformationFactory Factory = new();

    private static readonly ConcurrentDictionary<(int Source, int Target), ICoordinateTransformation> Transformations =
        new();

    public GeoPoint Transform(GeoPoint point, int targetEpsg)
    {
        if (point.Epsg == targetEpsg) return point;

        var transform = GetTransformation(point.Epsg, targetEpsg).MathTransform;
        var (x, y) = transform.Transform(point.X, point.Y);

        return new GeoPoint(x, y, targetEpsg);
    }

    public Geometry TransformGeometry(Geometry geometry, int sourceEpsg, int targetEpsg, int decimals = 8)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (sourceEpsg == targetEpsg)
        {
            // Samme system: bare merkingen kan mangle. Geometrien kopieres likevel,
            // slik at kallstedet aldri får tilbake sin egen instans med ny SRID.
            var uendret = geometry.Copy();
            uendret.SRID = targetEpsg;
            return uendret;
        }

        var resultat = geometry.Copy();
        resultat.Apply(new TransformFilter(GetTransformation(sourceEpsg, targetEpsg).MathTransform, decimals));
        resultat.SRID = targetEpsg;

        return resultat;
    }

    private static ICoordinateTransformation GetTransformation(int sourceEpsg, int targetEpsg)
    {
        return Transformations.GetOrAdd((sourceEpsg, targetEpsg), nøkkel =>
        {
            var kilde = CoordinateSystemRegister.Get(nøkkel.Source)
                        ?? throw new NotSupportedException(
                            $"EPSG {nøkkel.Source} finnes ikke i CoordinateSystemRegister.");
            var mål = CoordinateSystemRegister.Get(nøkkel.Target)
                      ?? throw new NotSupportedException(
                          $"EPSG {nøkkel.Target} finnes ikke i CoordinateSystemRegister.");

            return Factory.CreateFromCoordinateSystems(kilde, mål);
        });
    }

    /// <summary>
    /// Transformerer hvert punkt i en geometri.
    ///
    /// Går gjennom <c>CoordinateSequence</c> og ikke <c>Geometry.Coordinates</c>:
    /// sistnevnte gir et nytt array for flere sekvenstyper, og da ville endringene
    /// blitt skrevet til en kopi ingen ser. Artskart 2 muterer <c>Coordinates</c>
    /// direkte og slipper unna fordi standardfabrikken der deler arrayet.
    /// </summary>
    private sealed class TransformFilter(MathTransform transform, int decimals) : ICoordinateSequenceFilter
    {
        public bool Done => false;

        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var (x, y) = transform.Transform(seq.GetX(i), seq.GetY(i));

            seq.SetX(i, Math.Round(x, decimals, MidpointRounding.ToEven));
            seq.SetY(i, Math.Round(y, decimals, MidpointRounding.ToEven));
        }
    }
}

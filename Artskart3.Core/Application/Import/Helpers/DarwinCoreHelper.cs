using System.Text.RegularExpressions;

namespace Artskart3.Core.Application.Import.Helpers;

/// <summary>
/// Port av Artskart 2s <c>DarwinCoreHelper</c>.
/// </summary>
public static class DarwinCoreHelper
{
    /// <summary>
    /// Ti tegn eller mindre, bare siffer: et løpenummer, ikke en identifikator vi kan
    /// stole på på tvers av kilder.
    /// </summary>
    private static readonly Regex BareSiffer = new(@"^\d{1,10}$", RegexOptions.Compiled);

    /// <summary>
    /// Nøkkelen som binder en cache-post til en observasjon: <c>Observation.ProxyId</c>.
    /// Endres den, mister importen koblingen til 61 millioner eksisterende rader.
    ///
    /// To former:
    ///
    /// 1. <c>occurrenceId</c> i lowercase — når den finnes, ikke er et rent løpenummer
    ///    på 1–10 siffer, og ikke er den samme som katalognummeret.
    /// 2. <c>institusjon/samling/katalognummer</c> ellers.
    ///
    /// TRE DETALJER SOM MÅ VÆRE SOM DE ER:
    ///
    /// <c>occurrenceId</c> returneres uten <c>Trim()</c>. Sammenligningen mot
    /// katalognummeret trimmer, men returverdien gjør det ikke — i Artskart 2 binder
    /// <c>.Trim()</c> seg til sammenligningen alene.
    ///
    /// Bare katalognummeret lowercases i form 2. I Artskart 2 henger
    /// <c>.ToLowerInvariant()</c> på det siste leddet i konkatenasjonen, ikke på hele
    /// strengen. At institusjon og samling likevel står med store bokstaver i basen
    /// skyldes at <c>ImportRecordId</c> har kjørt <c>ToUpperInvariant()</c> på dem før
    /// de kommer hit.
    ///
    /// <c>|</c> byttes bare i katalognummeret, <c>/</c> i alle tre.
    /// </summary>
    public static string CleanDarwinCoreId(
        string? institutionCode,
        string? collectionCode,
        string? catalogNumber,
        string? occurenceId)
    {
        if (!string.IsNullOrWhiteSpace(occurenceId)
            && !BareSiffer.IsMatch(occurenceId)
            && (string.IsNullOrWhiteSpace(catalogNumber)
                || occurenceId.ToLowerInvariant().Trim() != catalogNumber.ToLowerInvariant().Trim()))
        {
            return occurenceId.ToLowerInvariant();
        }

        return EmptyIfNull(institutionCode).Replace('/', '_').Trim()
               + "/"
               + EmptyIfNull(collectionCode).Replace('/', '_').Trim()
               + "/"
               + EmptyIfNull(catalogNumber).Replace('/', '_').Trim().Replace('|', '_').ToLowerInvariant();
    }

    private static string EmptyIfNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value;
}

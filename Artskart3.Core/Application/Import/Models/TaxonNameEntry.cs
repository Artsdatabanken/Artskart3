using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Helpers;

namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Ett navn i taksonomien, slik navneoppslaget trenger det. Port av Artskart 2s
/// <c>TaxonSearchService.SciName</c>.
///
/// Rangen er med på navnet og ikke bare på taksonet fordi oppslaget avgjør flertydige
/// treff på nettopp rang — det er billigere å denormalisere den inn enn å slå opp
/// taksonet for hver kandidat.
/// </summary>
public class TaxonNameEntry
{
    private static readonly Regex HtmlTagg = new("<[^>]*>", RegexOptions.Compiled);

    /// <summary>
    /// Navn som bare er en rangforkortelse sier ingenting og skal ikke kunne slås opp.
    /// </summary>
    private static readonly string[] BarePrefiks = ["sp", "indet", "var", "spp", "ssp", "subsp", "agg", "×"];

    public required int NameId { get; init; }

    public required int TaxonId { get; init; }

    /// <summary>Om navnet er det gyldige for taksonet, til forskjell fra et synonym.</summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Navnet slik det står i basen — med HTML-formatering. Taksonomien fra NorTaxa
    /// leverer <c>&lt;i&gt;Vulpes vulpes&lt;/i&gt;</c>, og det er derfor
    /// <see cref="ScientificNameFull"/> finnes.
    /// </summary>
    public required string ScientificNameFormatted { get; init; }

    public string? ScientificNameAuthorship { get; init; }

    /// <summary>Rangnavnet i små bokstaver, f.eks. <c>species</c>.</summary>
    public required string RankName { get; init; }

    /// <summary>
    /// Lavere tall er høyere rang. Brukes til å plukke den høyeste rangen når et navn
    /// treffer flere taksoner.
    /// </summary>
    public required int RankId { get; init; }

    /// <summary>Navnet uten HTML-formatering.</summary>
    public string ScientificNameFull => HtmlTagg.Replace(ScientificNameFormatted, string.Empty);

    /// <summary>
    /// Navnet, eller <c>null</c> hvis det bare er en rangforkortelse. Målt i den
    /// produksjonslike basen: 0 av 228 639 navn er det — vernet er for dataene som kan
    /// komme, ikke for dem som er der. Artskart 2 har ingen nullsjekk her og ville
    /// kastet ved oppbyggingen av oppslaget.
    /// </summary>
    public string? ScientificName
    {
        get
        {
            var navn = ScientificNameFull;

            if (string.IsNullOrWhiteSpace(navn)) return navn;

            var nøkkel = navn.ToLowerInvariant().Trim().Replace(".", string.Empty);

            return BarePrefiks.Contains(nøkkel) ? null : navn;
        }
    }

    /// <summary>
    /// Navn og forfatter satt sammen, slik kilder ofte oppgir det:
    /// <c>Vulpes vulpes Linnaeus, 1758</c>.
    /// </summary>
    public string CompositeName =>
        ScientificName + (string.IsNullOrWhiteSpace(ScientificNameAuthorship)
            ? string.Empty
            : " " + ScientificNameAuthorship);

    /// <summary>
    /// Kopi med parentesinnholdet fjernet. Artskart 2 legger en slik ekstra oppføring i
    /// oppslaget for hvert navn med nøyaktig én parentes, slik at
    /// <c>Mergus (Mergellus) albellus</c> også kan slås opp som <c>Mergus albellus</c>.
    /// Navn med to parenteser hoppes over — <c>mus (mus) (domesticus)</c> ville blitt
    /// meningsløst.
    /// </summary>
    public TaxonNameEntry UtenParentesinnhold() => new()
    {
        NameId = NameId,
        TaxonId = TaxonId,
        Accepted = Accepted,
        ScientificNameFormatted = TaxonomyHelper.RemoveScientificNameParanteseContent(ScientificNameFormatted),
        ScientificNameAuthorship = ScientificNameAuthorship,
        RankName = RankName,
        RankId = RankId
    };

    public override string ToString() => $"({NameId}){CompositeName}";
}

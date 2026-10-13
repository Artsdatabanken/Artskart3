using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Slår artsnavn opp i taksonomien. Port av Artskart 2s <c>TaxonSearchService</c>.
///
/// TO OPPSLAG, BYGGET ÉN GANG:
///
/// <list type="bullet">
/// <item><c>ByCompositeName</c> — navn pluss forfatter, slik kilder ofte oppgir det:
/// «vulpes vulpes linnaeus, 1758»</item>
/// <item><c>ByScientificName</c> — navnet alene</item>
/// </list>
///
/// Begge nøkles med <c>TaxonomyHelper.CleanScientificName</c>, den samme
/// normaliseringen importen kjører på kildens navn. Det er derfor nøklene matcher i
/// det hele tatt.
///
/// KASKADEN HAR SEKS LEDD, og rekkefølgen er det som avgjør <c>TaxonId</c> for 60
/// millioner poster:
///
/// <list type="number">
/// <item>Navn + forfatter, eksakt</item>
/// <item>Navn + forfatter uten parentesinnhold</item>
/// <item>Navnet alene, med forfatteren som tiebreaker</item>
/// <item>Forkortet navn for underart, varietet, form og art — de tre eller to første
/// leddene</item>
/// </list>
///
/// Innenfor hvert ledd løses flertydighet i samme rekkefølge: ett treff vinner, så
/// rangen kilden oppgav, så gyldig navn framfor synonym, så høyeste rang. Står to
/// kandidater likt på rang til slutt, gis det opp — og kandidatene legges i
/// <c>Diagnostics</c>, som er det eneste sporet av hvorfor navnet ikke kunne bestemmes.
///
/// TAKSONOMIEN LIGGER I MINNET. Artskart 2 gjør det samme for navnene, men slår opp
/// selve taksonet i basen for hvert treff (<c>QuickGetTaxon</c>). Her er også taksonene
/// i minnet, så et oppslag koster ingen databaserunde.
///
/// MÅLT MOT <c>Artskart3IndexProdLikeTestMigrations</c>
/// (<c>TaxonNameResolverSmokeTests</c>):
///
/// <list type="bullet">
/// <item>152 756 taksoner, 228 639 navn, 16,8 MB navnetekst</item>
/// <item>Lasting og oppbygging av begge oppslagene: 1,4 s</item>
/// <item>Minne: 155 MB</item>
/// <item>0,10 ms per oppslag</item>
/// <item>20 000 ekte navn fra basen slått opp igjen: 99,59 % treff. De 81 som ikke
/// kunne bestemmes er «Incertae sedis» — plassholderen for usikker plassering, delt av
/// 262 ulike taksoner. Den kan ikke og skal ikke bestemmes entydig</item>
/// </list>
///
/// LASTES VED FØRSTE BRUK, og lastingen er tråd-trygg. Samme begrunnelse som for
/// <see cref="GeographyService"/>: en worker som ikke importerer skal ikke bruke minnet.
/// </summary>
public class TaxonNameResolver : ITaxonNameResolver
{
    private readonly ITaxonomySource _source;
    private readonly ILogger<TaxonNameResolver> _logger;
    private readonly Lock _låse = new();

    private ILookup<string, TaxonNameEntry>? _påSammensattNavn;
    private ILookup<string, TaxonNameEntry>? _påNavn;
    private Dictionary<int, TaxonRecord>? _taksoner;

    public TaxonNameResolver(ITaxonomySource source, ILogger<TaxonNameResolver> logger)
    {
        _source = source;
        _logger = logger;
    }

    public TaxonMatch? Resolve(string name, string? recordTaxonRank, string? authorString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Last();

        var diagnostikk = string.Empty;

        // 1. Navn og forfatter som kilden oppgav det.
        var treff = Velg(_påSammensattNavn![name].ToArray(), recordTaxonRank, ref diagnostikk);
        if (treff is not null) return treff with { Diagnostics = diagnostikk };

        // 2. Samme, men uten parentesinnhold: «mergus (mergellus) albellus» finnes også
        //    som «mergus albellus».
        var utenParenteser = TaxonomyHelper.RemoveScientificNameParanteseContent(name);
        treff = Velg(_påSammensattNavn[utenParenteser].ToArray(), recordTaxonRank, ref diagnostikk);
        if (treff is not null) return treff with { Diagnostics = diagnostikk };

        // 3. Navnet alene, med forfatteren som tiebreaker.
        treff = VelgPåNavn(utenParenteser, recordTaxonRank, authorString, ref diagnostikk);
        if (treff is not null) return treff with { Diagnostics = diagnostikk };

        // 4. Forkortet navn. En underart oppgitt med fire ledd kan være art pluss
        //    forfatter; tre ledd er det underarten faktisk heter.
        var ledd = utenParenteser.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var antallLedd = recordTaxonRank switch
        {
            "subspecies" or "variety" or "form" when ledd.Length > 3 => 3,
            "species" when ledd.Length > 2 => 2,
            _ => 0
        };

        if (antallLedd > 0)
        {
            var forkortet = string.Join(" ", ledd.Take(antallLedd));
            treff = VelgPåNavn(forkortet, recordTaxonRank, authorString, ref diagnostikk);
            if (treff is not null) return treff with { Diagnostics = diagnostikk };
        }

        return null;
    }

    /// <summary>
    /// Plukker ett navn blant treffene på det sammensatte navnet: ett treff vinner, så
    /// rangen kilden oppgav, så høyeste rang hvis den er entydig.
    /// </summary>
    private TaxonMatch? Velg(TaxonNameEntry[] kandidater, string? recordTaxonRank, ref string diagnostikk)
    {
        if (kandidater.Length == 0) return null;

        if (kandidater.Length == 1) return Treff(kandidater[0]);

        if (!string.IsNullOrWhiteSpace(recordTaxonRank))
        {
            var påRang = PåRang(kandidater, recordTaxonRank);
            if (påRang.Length == 1) return Treff(påRang[0]);
        }

        var etterRang = kandidater.OrderBy(x => x.RankId).ToArray();
        if (etterRang[0].RankId != etterRang[1].RankId) return Treff(etterRang[0]);

        diagnostikk = "Multiple hits: " + string.Join(",", kandidater.Select(x => x.ToString()));

        return null;
    }

    /// <summary>
    /// Plukker ett navn blant treffene på navnet alene. Her er forfatterstrengen med, og
    /// rekkefølgen er en annen enn i <see cref="Velg"/>: gyldig navn veier tyngre enn
    /// rang.
    /// </summary>
    private TaxonMatch? VelgPåNavn(
        string navn,
        string? recordTaxonRank,
        string? authorString,
        ref string diagnostikk)
    {
        // Artskart 2 nullstiller meldingen her. En «Multiple hits» fra leddene foran blir
        // altså borte når dette leddet kjører — bevart, fordi meldingen ellers ville
        // sagt noe annet enn det siste leddet som faktisk prøvde.
        diagnostikk = string.Empty;

        var kandidater = _påNavn![navn].ToArray();

        if (kandidater.Length == 0) return null;

        if (kandidater.Length == 1) return Treff(kandidater[0]);

        // Forfatteren sammenlignes i små bokstaver. Målt: 0 av 228 639 navn har NULL
        // som forfatter (29 146 har tom streng), så Artskart 2s manglende nullsjekk er
        // latent — den er sikret her uten at oppførselen endres.
        var medForfatter = !string.IsNullOrWhiteSpace(authorString)
            ? kandidater.Where(x =>
                string.Equals(x.ScientificNameAuthorship, authorString, StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : [];

        if (medForfatter.Length > 0)
        {
            if (medForfatter.Length == 1) return Treff(medForfatter[0]);

            var gyldige = medForfatter.Where(x => x.Accepted).ToArray();
            if (gyldige.Length == 1) return Treff(gyldige[0]);

            // Ingen gyldige, men alle peker på samme takson: da er det samme hvilket
            // navn vi tar. Artskart 2 tar det første av ALLE kandidatene her, ikke av
            // dem som matchet forfatteren — bevart.
            if (gyldige.Length == 0 && medForfatter.All(x => x.TaxonId == medForfatter[0].TaxonId))
            {
                return Treff(kandidater[0]);
            }

            if (!string.IsNullOrWhiteSpace(recordTaxonRank))
            {
                var påRang = PåRang(kandidater, recordTaxonRank);
                if (påRang.Length == 1) return Treff(påRang[0]);
            }

            diagnostikk = "Multiple with author hits: " + string.Join(",", kandidater.Select(x => x.ToString()));

            return null;
        }

        var gyldigeUtenForfatter = kandidater.Where(x => x.Accepted).ToArray();
        if (gyldigeUtenForfatter.Length == 1) return Treff(gyldigeUtenForfatter[0]);

        if (gyldigeUtenForfatter.Length == 0 && kandidater.All(x => x.TaxonId == kandidater[0].TaxonId))
        {
            return Treff(kandidater[0]);
        }

        if (!string.IsNullOrWhiteSpace(recordTaxonRank))
        {
            var påRang = PåRang(kandidater, recordTaxonRank);
            if (påRang.Length == 1) return Treff(påRang[0]);
        }

        var etterRang = kandidater.OrderBy(x => x.RankId).ToArray();
        if (etterRang[0].RankId != etterRang[1].RankId) return Treff(etterRang[0]);

        diagnostikk = "Multiple hits: " + string.Join(",", kandidater.Select(x => x.ToString()));

        return null;
    }

    private static TaxonNameEntry[] PåRang(TaxonNameEntry[] kandidater, string recordTaxonRank)
    {
        var rang = recordTaxonRank.Trim().ToLowerInvariant();

        return kandidater.Where(x => x.RankName == rang).ToArray();
    }

    /// <summary>
    /// Binder navnetreffet til taksonet. Mangler taksonet — noe som bare kan skje hvis
    /// navnet peker på en slettet rad — er treffet verdiløst.
    /// </summary>
    private TaxonMatch? Treff(TaxonNameEntry navn)
    {
        if (!_taksoner!.TryGetValue(navn.TaxonId, out var takson))
        {
            _logger.LogWarning(
                "Navn {NameId} peker på takson {TaxonId} som ikke finnes i taksonomien",
                navn.NameId, navn.TaxonId);

            return null;
        }

        return new TaxonMatch(takson, navn.NameId, navn.ScientificNameFormatted, string.Empty);
    }

    private void Last()
    {
        if (_taksoner is not null) return;

        lock (_låse)
        {
            if (_taksoner is not null) return;

            var klokke = System.Diagnostics.Stopwatch.StartNew();
            var snapshot = _source.LoadAll();

            var navn = new List<TaxonNameEntry>(snapshot.Names.Count);

            foreach (var oppføring in snapshot.Names)
            {
                // Navn som bare er en rangforkortelse kan ikke slås opp på noe.
                if (oppføring.ScientificName is null) continue;

                navn.Add(oppføring);

                // Ett parentesnivå får en ekstra oppføring uten parentesinnholdet. Mer
                // enn ett gir et navn ingen leter etter.
                if (oppføring.ScientificNameFull.Count(x => x == '(') == 1)
                {
                    var uten = oppføring.UtenParentesinnhold();

                    if (uten.ScientificName is not null) navn.Add(uten);
                }
            }

            // Nøklene normaliseres med den samme funksjonen importen kjører på kildens
            // navn — ellers ville ingenting matchet.
            _påSammensattNavn = navn.ToLookup(x =>
                TaxonomyHelper.CleanScientificName(x.CompositeName.ToLowerInvariant()));

            _påNavn = navn.ToLookup(x =>
                TaxonomyHelper.CleanScientificName(x.ScientificName!.ToLowerInvariant()));

            _taksoner = snapshot.Taxa.ToDictionary(x => x.Id);

            klokke.Stop();

            _logger.LogInformation(
                "Lastet taksonomien: {TaxonCount} taksoner, {NameCount} navneoppføringer "
                + "({CompositeKeys} sammensatte nøkler, {NameKeys} navnenøkler) på {ElapsedMs} ms",
                _taksoner.Count, navn.Count, _påSammensattNavn.Count, _påNavn.Count, klokke.ElapsedMilliseconds);
        }
    }
}

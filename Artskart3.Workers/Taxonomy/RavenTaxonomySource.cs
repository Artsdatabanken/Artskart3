using System.Runtime.CompilerServices;
using System.Text.Json;
using Artskart3.Core.Application.Taxonomy;

namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Leser taksonomidokumenter fra Artskart 2s RavenDB over HTTP.
///
/// HVORFOR IKKE RAVEN-KLIENTEN. Artskart 2 bruker <c>RavenDB.Client</c> 3.5.9. Den har
/// et <c>netstandard1.3</c>-bygg som .NET 10 kan referere, men det drar inn fjorten
/// foreldede <c>System.*</c>-pakker fra 4.3.0-generasjonen. Dokumentene er flate JSON,
/// og REST-API-et i Raven 3.5 er nok — da er det ingenting å bytte ut senere, bare denne
/// mappen å slette.
///
/// ALT RAVEN-SPESIFIKT LIGGER HER. Resten av løsningen kjenner bare
/// <see cref="ITaxonomySource"/>.
///
/// STRØMMEENDEPUNKTET ER IKKE VERIFISERT ENNÅ. <c>/streams/docs?startsWith=</c> er
/// Raven 3.5s måte å lese en hel samling uten paging, men serveren har ikke vært
/// tilgjengelig under skrivingen. Prefiksen er derfor konfigurerbar, og
/// <see cref="TaxonomyOptions.UseIndexQuery"/> slår over til samme spørring Artskart 2
/// bruker (<c>Raven/DocumentsByEntityName</c> med <c>Tag:</c>) om strømmen ikke svarer
/// som forventet.
/// </summary>
public sealed class RavenTaxonomySource : ITaxonomySource
{
    private readonly HttpClient _klient;
    private readonly TaxonomyOptions _innstillinger;
    private readonly ILogger<RavenTaxonomySource> _logger;

    public RavenTaxonomySource(
        HttpClient klient,
        TaxonomyOptions innstillinger,
        ILogger<RavenTaxonomySource> logger)
    {
        _klient = klient;
        _innstillinger = innstillinger;
        _logger = logger;
    }

    public string Describe() =>
        $"RavenDB {_innstillinger.BaseAddress} database {_innstillinger.Database}";

    public async Task<IReadOnlyList<SpeciesGroupDocument>> ReadSpeciesGroupsAsync(
        CancellationToken cancellationToken)
    {
        var grupper = new List<SpeciesGroupDocument>();

        await foreach (var gruppe in LesSamling<SpeciesGroupDocument>(
                           _innstillinger.SpeciesGroupCollection, cancellationToken))
        {
            // Nøkkelen først, RId som reserve — kroppen har RId der Id mangler.
            gruppe.Id = IdFraNøkkel(gruppe.DocumentKey) ?? gruppe.RId;

            if (gruppe.Id == 0)
            {
                _logger.LogWarning(
                    "Artsgruppe uten brukbar id, hoppes over. Nøkkel {Noekkel}", gruppe.DocumentKey);

                continue;
            }

            grupper.Add(gruppe);
        }

        _logger.LogInformation("Leste {Antall} artsgrupper fra {Kilde}", grupper.Count, Describe());

        return grupper;
    }

    public async IAsyncEnumerable<TaxonInfoDocument> ReadTaxaAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var antall = 0;

        var avvik = 0;

        await foreach (var takson in LesSamling<TaxonInfoDocument>(
                           _innstillinger.TaxonCollection, cancellationToken))
        {
            var fraNøkkel = IdFraNøkkel(takson.DocumentKey);

            if (fraNøkkel is null)
            {
                throw new InvalidOperationException(
                    $"Takson uten brukbar dokumentnøkkel: «{takson.DocumentKey}». "
                    + "Id-en finnes ikke i JSON-kroppen, så uten nøkkelen er dokumentet ubrukelig.");
            }

            takson.Id = fraNøkkel.Value;

            // KONTROLL, IKKE ANTAKELSE. Kroppens TaxonId skal være samme tall som
            // nøkkelen. Er de ulike, er det nøkkelen som gjelder — det er den Artskart 2
            // bruker som Taxon.Id — men da vil vi vite om det.
            if (takson.TaxonId != 0 && takson.TaxonId != takson.Id && avvik++ < 10)
            {
                _logger.LogWarning(
                    "Takson {Noekkel} har TaxonId {TaxonId} i kroppen. Nøkkelen brukes.",
                    takson.DocumentKey, takson.TaxonId);
            }

            antall++;
            yield return takson;
        }

        if (avvik > 0)
        {
            _logger.LogWarning("{Antall} taksoner hadde TaxonId ulik dokumentnøkkelen", avvik);
        }

        _logger.LogInformation("Leste {Antall} taksoner fra {Kilde}", antall, Describe());
    }

    /// <summary>
    /// Strømmer én samling. Dokumentene kommer som <c>{"Results":[ … ]}</c>, og de leses
    /// ett og ett — 180 000 taksoner med navnelister inni skal ikke bufres i minne.
    /// </summary>
    private async IAsyncEnumerable<T> LesSamling<T>(
        string samling,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : ISourceDocument
    {
        var adresse = _innstillinger.UseIndexQuery
            ? $"databases/{_innstillinger.Database}/indexes/Raven/DocumentsByEntityName"
              + $"?query=Tag:{Uri.EscapeDataString(samling)}&pageSize={_innstillinger.PageSize}"
            : $"databases/{_innstillinger.Database}/streams/docs"
              + $"?startsWith={Uri.EscapeDataString(samling + "/")}";

        using var svar = await _klient.GetAsync(
            adresse, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!svar.IsSuccessStatusCode)
        {
            var kropp = await svar.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"RavenDB svarte {(int)svar.StatusCode} på {adresse}. "
                + $"Kropp: {Kort(kropp)}");
        }

        await using var strøm = await svar.Content.ReadAsStreamAsync(cancellationToken);

        var dokument = await JsonDocument.ParseAsync(strøm, cancellationToken: cancellationToken);

        if (!dokument.RootElement.TryGetProperty("Results", out var resultater))
        {
            throw new InvalidOperationException(
                $"RavenDB-svaret på {adresse} manglet «Results». "
                + "Endepunktformen må verifiseres mot serveren.");
        }

        foreach (var element in resultater.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tolket = element.Deserialize<T>(TaxonomyJson.Options);

            if (tolket is null) continue;

            tolket.DocumentKey = Nøkkel(element);

            yield return tolket;
        }
    }

    /// <summary>
    /// Dokumentnøkkelen fra metadataene.
    ///
    /// DEN MÅ HENTES HERFRA. Verken <c>TaxonInfos</c> eller <c>SpeciesGroups</c> har et
    /// <c>Id</c>-felt i JSON-kroppen — målt mot den frosne kopien. Artskart 2s klient
    /// fyller <c>Id</c> fra nøkkelen gjennom sin id-konvensjon, og over rått HTTP må det
    /// gjøres her, ellers blir hver id 0.
    /// </summary>
    private static string? Nøkkel(JsonElement element) =>
        element.TryGetProperty("@metadata", out var metadata)
        && metadata.TryGetProperty("@id", out var id)
            ? id.GetString()
            : null;

    /// <summary>
    /// Talldelen av en nøkkel som <c>TaxonInfos/47871</c>.
    /// </summary>
    private static int? IdFraNøkkel(string? nøkkel)
    {
        if (string.IsNullOrWhiteSpace(nøkkel)) return null;

        var skille = nøkkel.LastIndexOf('/');

        return skille >= 0 && int.TryParse(nøkkel[(skille + 1)..], out var tall) ? tall : null;
    }

    private static string Kort(string tekst) => tekst.Length <= 200 ? tekst : tekst[..200] + "…";

}

